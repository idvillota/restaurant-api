using System.Text.Json;
using Restaurant.Domain.Common;

namespace Restaurant.Application.Features.KitchenPrinters;

public sealed class HostPrintAgentConfigSnapshot
{
    public required string ApiUrl { get; init; }
    public required string WorkRoot { get; init; }
    public required string ConnectionString { get; init; }
    public required string ContainerName { get; init; }
    public required Guid TenantId { get; init; }
    public required IReadOnlyDictionary<string, string> Printers { get; init; }
    public string SumatraPath { get; init; } = "SumatraPDF.exe";
}

/// <summary>Arma el JSON del agente de PC. No conoce HTTP ni el ZIP.</summary>
public static class HostPrintAgentConfigBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static IReadOnlyDictionary<string, string> MapPrinters(
        IReadOnlyList<HostPrintAgentPrinterDto> printers,
        IReadOnlySet<string> knownStationCodes)
    {
        if (printers.Count == 0)
            throw new InvalidOperationException("Configure al menos una impresora de Windows.");

        var allowedCodes = new HashSet<string>(knownStationCodes, StringComparer.OrdinalIgnoreCase)
        {
            KitchenPrinterDefaults.ReceiptStationCode,
        };
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var claimedStations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var printer in printers)
        {
            var windowsName = printer.WindowsPrinterName.Trim();
            if (windowsName.Length == 0)
                throw new InvalidOperationException("El nombre de impresora de Windows no puede estar vacío.");

            if (map.ContainsKey(windowsName))
                throw new InvalidOperationException($"La impresora '{windowsName}' está repetida.");

            var codes = new List<string>();
            foreach (var raw in printer.StationCodes)
            {
                var code = NormalizeStationCode(raw);
                if (code.Length == 0)
                    continue;

                if (!allowedCodes.Contains(code))
                    throw new InvalidOperationException($"La estación '{code}' no existe en este local.");

                if (!claimedStations.Add(code))
                    throw new InvalidOperationException($"La estación '{code}' ya está asignada a otra impresora.");

                codes.Add(code);
            }

            if (codes.Count == 0)
                throw new InvalidOperationException(
                    $"'{windowsName}' no tiene estaciones. Indique qué debe imprimir.");

            map[windowsName] = string.Join(',', codes);
        }

        return map;
    }

    public static string ToJson(HostPrintAgentConfigSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.ConnectionString))
            throw new InvalidOperationException(
                "Falta GeneratedFiles:AzureBlob:ConnectionString en el API. El agente de PC copia los PDF desde Azure.");

        var document = new AgentConfigFile
        {
            ApiUrl = snapshot.ApiUrl,
            WorkRoot = snapshot.WorkRoot,
            Copy = new AgentCopyFile
            {
                ConnectionString = snapshot.ConnectionString,
                ContainerName = snapshot.ContainerName,
                TenantId = snapshot.TenantId.ToString(),
            },
            Print = new AgentPrintFile
            {
                Simulate = false,
                SumatraPath = snapshot.SumatraPath,
                Printers = new Dictionary<string, string>(snapshot.Printers, StringComparer.OrdinalIgnoreCase),
            },
        };

        return JsonSerializer.Serialize(document, JsonOptions);
    }

    private static string NormalizeStationCode(string? value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant();

    private sealed class AgentConfigFile
    {
        public string ApiUrl { get; set; } = "";
        public string WorkRoot { get; set; } = "";
        public AgentCopyFile Copy { get; set; } = new();
        public AgentPrintFile Print { get; set; } = new();
    }

    private sealed class AgentCopyFile
    {
        public string ConnectionString { get; set; } = "";
        public string ContainerName { get; set; } = "generated-files";
        public string TenantId { get; set; } = "";
        public string LocalFolder { get; set; } = "toPrint";
        public string StateFile { get; set; } = "copy-state.json";
        public int PollIntervalSeconds { get; set; } = 5;
    }

    private sealed class AgentPrintFile
    {
        public bool Simulate { get; set; }
        public string SumatraPath { get; set; } = "SumatraPDF.exe";
        public string PrintedFolder { get; set; } = "Printed";
        public string ErrorFolder { get; set; } = "error";
        public Dictionary<string, string> Printers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
