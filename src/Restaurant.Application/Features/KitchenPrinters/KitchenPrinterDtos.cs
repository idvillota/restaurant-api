using System.ComponentModel.DataAnnotations;

namespace Restaurant.Application.Features.KitchenPrinters;

public sealed class PrinterStationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public bool IsSystemDefault { get; set; }
}

public sealed class CreatePrinterStationDto
{
    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string Code { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}

public sealed class UpdatePrinterStationDto
{
    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(32)]
    public string? Code { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class ProductTypePrinterRoutingItemDto
{
    public Guid ProductTypeId { get; set; }
    public string ProductTypeName { get; set; } = string.Empty;
    public bool ProductTypeIsActive { get; set; }
    public Guid? PrinterStationId { get; set; }
    public string? PrinterStationName { get; set; }
    public string? PrinterStationCode { get; set; }
}

public sealed class ProductTypePrinterRoutingDto
{
    public IReadOnlyList<PrinterStationDto> Stations { get; set; } = [];
    public IReadOnlyList<ProductTypePrinterRoutingItemDto> Items { get; set; } = [];
}

public sealed class UpdateProductTypePrinterRoutingDto
{
    [Required]
    public List<UpdateProductTypePrinterRoutingItemDto> Items { get; set; } = [];
}

public sealed class UpdateProductTypePrinterRoutingItemDto
{
    [Required]
    public Guid ProductTypeId { get; set; }

    public Guid? PrinterStationId { get; set; }
}
