using System.ComponentModel.DataAnnotations;
using Restaurant.Domain.Enums;

namespace Restaurant.Application.Features.Procurement.Purchases;

public sealed class PurchaseDto
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string BillNumber { get; set; } = string.Empty;
    public DateTime PurchasedAtUtc { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseLineDto> Lines { get; set; } = [];
}

public sealed class PurchaseLineDto
{
    public Guid Id { get; set; }
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public IngredientUnit IngredientUnit { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class PurchaseListItemDto
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string BillNumber { get; set; } = string.Empty;
    public DateTime PurchasedAtUtc { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public int LineCount { get; set; }
}

public sealed class CreatePurchaseLineDto
{
    [Required]
    public Guid IngredientId { get; set; }

    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }
}

public sealed class CreatePurchaseDto
{
    [Required]
    public Guid ProviderId { get; set; }

    [Required]
    [MaxLength(80)]
    public string BillNumber { get; set; } = string.Empty;

    [Required]
    public DateTime? PurchasedAtUtc { get; set; }

    [Range(0, double.MaxValue)]
    public decimal TaxAmount { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [MinLength(1)]
    public List<CreatePurchaseLineDto> Lines { get; set; } = [];
}
