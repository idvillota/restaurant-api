using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Restaurant.Application.Common.Interfaces;

namespace Restaurant.Api.MobileSync;

public sealed class MobileSaleChannel : IMobileSalePublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, WebSocket>> _sockets = new();

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

        var buffer = new byte[256];
        try
        {
            while (socket.State == WebSocketState.Open && !context.RequestAborted.IsCancellationRequested)
            {
                var received = await socket.ReceiveAsync(buffer, context.RequestAborted);
                if (received.MessageType == WebSocketMessageType.Close)
                    break;
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

    private static async Task SendAsync(
        ConcurrentDictionary<string, WebSocket> devices,
        byte[] payload,
        string? exceptDeviceId,
        CancellationToken cancellationToken)
    {
        foreach (var (deviceId, socket) in devices)
        {
            if (string.Equals(deviceId, exceptDeviceId, StringComparison.Ordinal))
                continue;
            if (socket.State != WebSocketState.Open)
                continue;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                await socket.SendAsync(payload, WebSocketMessageType.Text, true, timeout.Token);
            }
            catch (Exception)
            {
                devices.TryRemove(deviceId, out _);
            }
        }
    }
}
