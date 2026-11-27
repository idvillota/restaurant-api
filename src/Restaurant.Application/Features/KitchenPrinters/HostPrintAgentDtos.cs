using System.ComponentModel.DataAnnotations;

namespace Restaurant.Application.Features.KitchenPrinters;

public sealed class HostPrintAgentPrinterDto
{
    [Required]
    [MaxLength(200)]
    public string WindowsPrinterName { get; set; } = string.Empty;

    public List<string> StationCodes { get; set; } = [];
}

public sealed class HostPrintAgentPackageRequest
{
    [Required]
    [MinLength(1)]
    public List<HostPrintAgentPrinterDto> Printers { get; set; } = [];
}

public sealed class HostPrintAgentPackage
{
    public required byte[] Content { get; init; }

    public required string FileName { get; init; }

    public string? Token { get; init; }

    public bool IncludesRuntime { get; init; }

    public string ContentType { get; init; } = "application/octet-stream";
}

public sealed class HostPrintAgentSetupLinkDto
{
    public required string Token { get; init; }

    public required string FileName { get; init; }
}
