using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Options;
using Restaurant.Application.Features.Reports;

namespace Restaurant.Infrastructure.Services;

public sealed class StrategicAiInsightService : IStrategicAiInsightService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AzureOpenAiOptions _options;
    private readonly ILogger<StrategicAiInsightService> _logger;

    public StrategicAiInsightService(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureOpenAiOptions> options,
        ILogger<StrategicAiInsightService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<StrategicAiInsightsDto> GenerateInsightsAsync(
        StrategicJsonReportDocument document,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.DeploymentName))
            return new StrategicAiInsightsDto();

        var metricsSummary = BuildMetricsSummary(document);
        var payload = new
        {
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content =
                        """
                        Eres un consultor de restaurantes en Colombia. Responde ÚNICAMENTE con JSON válido (sin markdown).
                        Esquema exacto: {"insights":["string"],"recommendations":["string"]}
                        Máximo 5 insights y 5 recomendaciones, en español, accionables y basados solo en los datos provistos.
                        """,
                },
                new
                {
                    role = "user",
                    content = $"""
                        Informe: {document.Meta.Title}
                        Tipo: {document.Meta.ReportType}
                        Período: {document.Meta.StartDate:yyyy-MM-dd} a {document.Meta.EndDate:yyyy-MM-dd}
                        KPIs: {metricsSummary}
                        """,
                },
            },
            max_tokens = 1200,
            temperature = 0.2f,
            response_format = new { type = "json_object" },
        };

        var client = _httpClientFactory.CreateClient("AzureOpenAiClient");
        var deployment = _options.DeploymentName.Trim();
        var requestUri = BuildRequestUri(client, deployment);

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Azure OpenAI insights error {Status}: {Body}", response.StatusCode, body);
            return new StrategicAiInsightsDto();
        }

        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
            return new StrategicAiInsightsDto();

        try
        {
            var parsed = JsonSerializer.Deserialize<StrategicAiInsightsDto>(
                content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return parsed ?? new StrategicAiInsightsDto();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Respuesta JSON de insights no válida");
            return new StrategicAiInsightsDto();
        }
    }

    private static string BuildMetricsSummary(StrategicJsonReportDocument document)
    {
        var parts = document.Summary.Kpis
            .Select(k => $"{k.Label}: {k.Value}")
            .ToList();
        return string.Join("; ", parts);
    }

    private static string BuildRequestUri(HttpClient client, string deployment)
    {
        const string relative = "/openai/deployments/{0}/chat/completions?api-version=2024-02-15-preview";

        if (client.BaseAddress is null)
            return string.Format(relative, deployment);

        var basePath = client.BaseAddress.AbsolutePath.TrimEnd('/').ToLowerInvariant();
        var path = basePath.Contains("/openai")
            ? $"deployments/{deployment}/chat/completions?api-version=2024-02-15-preview"
            : string.Format(relative.TrimStart('/'), deployment);

        return new Uri(client.BaseAddress, path).ToString();
    }
}
