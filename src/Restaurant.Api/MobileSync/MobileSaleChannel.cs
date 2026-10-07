using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Application.Common.Interfaces;

namespace Restaurant.Api.MobileSync;

public sealed class MobileSaleChannel : IMobileSalePublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private const int CatchUpPage = 20;
    private const int CatchUpCap = 500;

    private readonly IServiceScopeFactory _scopes;
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, WebSocket>> _sockets = new();
    private readonly ConcurrentDictionary<string, DateTime> _printingSince = new();
    private readonly ConcurrentDictionary<WebSocket, SemaphoreSlim> _sendGates = new();

    public MobileSaleChannel(IServiceScopeFactory scopes) => _scopes = scopes;

    public async Task AcceptAsync(HttpContext context, ICurrentTenantContext tenant)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true || tenant.TenantId is not Guid tenantId)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var deviceId = context.Request.Query["deviceId"].ToString().Trim();
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var socket = await context.WebSockets.AcceptWebSocketAsync();
        var devices = _sockets.GetOrAdd(tenantId, _ => new ConcurrentDictionary<string, WebSocket>());
        devices[deviceId] = socket;

        var buffer = new byte[1024];
        var message = new StringBuilder();
        try
        {
            while (socket.State == WebSocketState.Open && !context.RequestAborted.IsCancellationRequested)
            {
                var received = await socket.ReceiveAsync(buffer, context.RequestAborted);
                if (received.MessageType == WebSocketMessageType.Close)
                    break;
                if (received.MessageType != WebSocketMessageType.Text)
                    continue;

                message.Append(Encoding.UTF8.GetString(buffer, 0, received.Count));
                if (!received.EndOfMessage)
                    continue;

                var text = message.ToString();
                message.Clear();
                if (text.Length > 256)
                    continue;
                await ApplyPrinterSignalAsync(tenantId, deviceId, text, context.RequestAborted);
            }
        }
        catch (OperationCanceledException)
        {
            // The tablet closed the channel.
        }
        catch (WebSocketException)
        {
            // The connection dropped. The next sale will wait for a new one.
        }
        finally
        {
            if (devices.TryGetValue(deviceId, out var current) && ReferenceEquals(current, socket))
                devices.TryRemove(deviceId, out _);
            _sendGates.TryRemove(socket, out _);

            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
        }
    }

    public async Task PublishAsync(
        Guid tenantId,
        MobileSaleNotice notice,
        string? exceptDeviceId,
        CancellationToken cancellationToken = default)
    {
        if (!_sockets.TryGetValue(tenantId, out var devices) || devices.IsEmpty || notice.Lines.Count == 0)
            return;

        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            type = "sale",
            remoteOrderId = notice.RemoteOrderId,
            tableId = notice.TableId,
            tableCode = notice.TableCode,
            waiterName = notice.WaiterName,
            cursor = notice.Cursor,
            lines = notice.Lines.Select(line => new
            {
                lineId = line.LineId,
                productId = line.ProductId,
                productName = line.ProductName,
                quantity = line.Quantity,
                unitPrice = line.UnitPrice,
                notes = line.Notes ?? string.Empty,
            }),
        }, JsonOptions));

        await SendAsync(devices, payload, exceptDeviceId, cancellationToken);
    }

    public async Task PublishTablesAvailableAsync(
        Guid tenantId,
        IReadOnlyList<Guid> tableIds,
        CancellationToken cancellationToken = default)
    {
        if (!_sockets.TryGetValue(tenantId, out var devices) || devices.IsEmpty || tableIds.Count == 0)
            return;

        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            type = "tablesAvailable",
            tableIds,
        }, JsonOptions));

        await SendAsync(devices, payload, null, cancellationToken);
    }

    public async Task PublishPrintJobAsync(
        Guid tenantId,
        Guid jobId,
        string kind,
        string payloadFormat,
        string payload,
        string? exceptDeviceId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_sockets.TryGetValue(tenantId, out var devices) || devices.IsEmpty)
            return;

        var ready = ReadyDevices(tenantId, devices, exceptDeviceId);
        if (ready.Count == 0)
            return;

        await SendAsync(ready, PrintPayload(jobId, kind, payloadFormat, payload), null, cancellationToken, TimeSpan.FromSeconds(8));
    }

    private async Task ApplyPrinterSignalAsync(
        Guid tenantId,
        string deviceId,
        string text,
        CancellationToken cancellationToken)
    {
        string? type;
        var fresh = false;
        try
        {
            using var document = JsonDocument.Parse(text);
            if (!document.RootElement.TryGetProperty("type", out var typeProperty))
                return;
            type = typeProperty.GetString();
            if (document.RootElement.TryGetProperty("fresh", out var freshProperty) &&
                freshProperty.ValueKind == JsonValueKind.True)
            {
                fresh = true;
            }
        }
        catch (JsonException)
        {
            return;
        }

        var key = ReadyKey(tenantId, deviceId);
        if (type == "printIdle")
        {
            _printingSince.TryRemove(key, out _);
            return;
        }

        if (type != "printReady")
            return;

        var since = fresh
            ? _printingSince[key] = DateTime.UtcNow
            : _printingSince.GetOrAdd(key, _ => DateTime.UtcNow);
        try
        {
            await DeliverPendingAsync(tenantId, deviceId, since, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // The jobs stay pending. The next printReady sends them again.
        }
    }

    private async Task DeliverPendingAsync(
        Guid tenantId,
        string deviceId,
        DateTime createdSinceUtc,
        CancellationToken cancellationToken)
    {
        if (!_sockets.TryGetValue(tenantId, out var devices) ||
            !devices.TryGetValue(deviceId, out var socket) ||
            socket.State != WebSocketState.Open)
        {
            return;
        }

        var target = new ConcurrentDictionary<string, WebSocket> { [deviceId] = socket };
        var skip = 0;
        while (skip < CatchUpCap && socket.State == WebSocketState.Open)
        {
            IReadOnlyList<PrintJobTicket> jobs;
            using (var scope = _scopes.CreateScope())
            {
                var queue = scope.ServiceProvider.GetRequiredService<IPrintJobQueue>();
                jobs = await queue.ListPendingSinceAsync(tenantId, createdSinceUtc, cancellationToken, skip);
            }

            if (jobs.Count == 0)
                return;

            foreach (var job in jobs)
            {
                if (socket.State != WebSocketState.Open)
                    return;
                await SendAsync(
                    target,
                    PrintPayload(job.Id, job.Kind, job.PayloadFormat, job.Payload),
                    null,
                    cancellationToken,
                    TimeSpan.FromSeconds(8));
            }

            if (jobs.Count < CatchUpPage)
                return;
            skip += jobs.Count;
        }
    }

    private ConcurrentDictionary<string, WebSocket> ReadyDevices(
        Guid tenantId,
        ConcurrentDictionary<string, WebSocket> devices,
        string? exceptDeviceId)
    {
        var ready = new ConcurrentDictionary<string, WebSocket>();
        foreach (var (deviceId, socket) in devices)
        {
            if (string.Equals(deviceId, exceptDeviceId, StringComparison.Ordinal))
                continue;
            if (socket.State != WebSocketState.Open)
                continue;
            if (_printingSince.ContainsKey(ReadyKey(tenantId, deviceId)))
                ready[deviceId] = socket;
        }

        return ready;
    }

    private static byte[] PrintPayload(Guid jobId, string kind, string payloadFormat, string payload)
    {
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            type = "print",
            id = jobId,
            kind,
            payloadFormat,
            payload,
        }, JsonOptions));
    }

    private static string ReadyKey(Guid tenantId, string deviceId) => tenantId.ToString("N") + "|" + deviceId;

    private async Task SendAsync(
        ConcurrentDictionary<string, WebSocket> devices,
        byte[] payload,
        string? exceptDeviceId,
        CancellationToken cancellationToken,
        TimeSpan? sendTimeout = null)
    {
        foreach (var (deviceId, socket) in devices)
        {
            if (string.Equals(deviceId, exceptDeviceId, StringComparison.Ordinal))
                continue;
            if (socket.State != WebSocketState.Open)
                continue;

            var gate = _sendGates.GetOrAdd(socket, _ => new SemaphoreSlim(1, 1));
            var entered = false;
            try
            {
                await gate.WaitAsync(cancellationToken);
                entered = true;
                if (socket.State != WebSocketState.Open)
                    continue;

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(sendTimeout ?? TimeSpan.FromSeconds(2));
                await socket.SendAsync(payload, WebSocketMessageType.Text, true, timeout.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // A failed send keeps the phone registered. Pending comandas go out on the next printReady.
            }
            finally
            {
                if (entered)
                    gate.Release();
            }
        }
    }
}
