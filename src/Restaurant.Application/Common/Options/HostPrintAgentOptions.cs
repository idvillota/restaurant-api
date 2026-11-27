namespace Restaurant.Application.Common.Options;

public sealed class HostPrintAgentOptions
{
    public const string SectionName = "HostPrintAgent";

    /// <summary>
    /// Carpeta con HostPrintService.exe y Sumatra (sin config.json).
    /// Relativa al content root del API, o absoluta.
    /// </summary>
    public string PayloadDirectory { get; set; } = "HostPrintAgent/payload";

    /// <summary>HostPrintSetup.exe genérico (sin config). Relativo al content root, o absoluto.</summary>
    public string SetupPath { get; set; } = "HostPrintAgent/setup/HostPrintSetup.exe";

    public string InstallDirectory { get; set; } = @"C:\Hinova\HostPrint";

    public string WorkRoot { get; set; } = @"C:\Hinova\printFiles";

    public string SumatraPath { get; set; } = "SumatraPDF.exe";
}
