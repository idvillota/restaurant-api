using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Options;
using Restaurant.Domain.Entities;

namespace Restaurant.Infrastructure.Services;

public sealed class WhatsAppCounterReadyNotifier : ICounterReadyNotifier
{
    public const string HttpClientName = "counter-ready";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CounterPickupOptions _options;
    private readonly ILogger<WhatsAppCounterReadyNotifier> _logger;

    public WhatsAppCounterReadyNotifier(
        IHttpClientFactory httpClientFactory,
        IOptions<CounterPickupOptions> options,
        ILogger<WhatsAppCounterReadyNotifier> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CounterReadyNotificationResult> NotifyReadyAsync(
        SalesOrder order,
        string tradeName,
        CancellationToken cancellationToken = default)
    {
        var recipient = CounterPhoneNumbers.ToWhatsAppRecipient(order.CounterPhone);
        if (recipient is null)
            return new CounterReadyNotificationResult(false, "El celular no sirve para WhatsApp.");

        var message =
            $"Pedido {order.Number} de {tradeName} está listo. Recógelo en el mostrador.";

        if (IsWhatsAppConfigured())
        {
            var whatsAppError = await SendWhatsAppAsync(recipient, order.Number, tradeName, cancellationToken);
            if (whatsAppError is null)
                return new CounterReadyNotificationResult(true, null);

            var sms = await TrySmsFallbackAsync(recipient, message, cancellationToken);
            return sms ?? new CounterReadyNotificationResult(false, whatsAppError);
        }

        var fallback = await TrySmsFallbackAsync(recipient, message, cancellationToken);
        return fallback ?? new CounterReadyNotificationResult(false, "WhatsApp no está configurado.");
    }

    private bool IsWhatsAppConfigured() =>
        _options.WhatsAppEnabled &&
        !string.IsNullOrWhiteSpace(_options.WhatsAppPhoneNumberId) &&
        !string.IsNullOrWhiteSpace(_options.WhatsAppAccessToken) &&
        !string.IsNullOrWhiteSpace(_options.WhatsAppTemplateName);

    private async Task<string?> SendWhatsAppAsync(
        string recipient,
        string orderNumber,
        string tradeName,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://graph.facebook.com/v21.0/{_options.WhatsAppPhoneNumberId.Trim()}/messages");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.WhatsAppAccessToken.Trim());
            request.Content = JsonContent.Create(new
            {
                messaging_product = "whatsapp",
                to = recipient,
                type = "template",
                template = new
                {
                    name = _options.WhatsAppTemplateName.Trim(),
                    language = new { code = string.IsNullOrWhiteSpace(_options.WhatsAppTemplateLanguage) ? "es" : _options.WhatsAppTemplateLanguage.Trim() },
                    components = new object[]
                    {
                        new
                        {
                            type = "body",
                            parameters = new object[]
                            {
                                new { type = "text", text = orderNumber },
                                new { type = "text", text = tradeName },
                            },
                        },
                    },
                },
            });

            using var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
                return null;

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "WhatsApp ready notice failed with status {StatusCode}",
                (int)response.StatusCode);
            return Trim($"WhatsApp respondió {(int)response.StatusCode}. {ExtractGraphError(body)}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "WhatsApp ready notice could not be sent.");
            return "No se pudo contactar a WhatsApp.";
        }
    }

    private async Task<CounterReadyNotificationResult?> TrySmsFallbackAsync(
        string recipient,
        string message,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(_options.SmsFallbackUrl, UriKind.Absolute, out var url) ||
            url.Scheme != Uri.UriSchemeHttps)
            return null;

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.PostAsJsonAsync(url, new { to = recipient, message }, cancellationToken);
            if (response.IsSuccessStatusCode)
                return new CounterReadyNotificationResult(true, null);

            _logger.LogWarning("SMS fallback failed with status {StatusCode}", (int)response.StatusCode);
            return new CounterReadyNotificationResult(false, "El aviso por SMS no se pudo enviar.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "SMS fallback could not be sent.");
            return new CounterReadyNotificationResult(false, "El aviso por SMS no se pudo enviar.");
        }
    }

    private static string ExtractGraphError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
                return message.GetString() ?? string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private static string Trim(string value) =>
        value.Length <= 300 ? value : value[..300];
}
