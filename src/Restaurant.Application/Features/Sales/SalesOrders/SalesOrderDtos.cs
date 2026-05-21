using System.ComponentModel.DataAnnotations;
using Restaurant.Domain.Enums;

namespace Restaurant.Application.Features.Sales.SalesOrders;

public sealed class SalesOrderDto
{
    public Guid Id { get; set; }
    public Guid? DiningTableId { get; set; }
    public string? DiningTableCode { get; set; }
    public string Number { get; set; } = string.Empty;
    public SalesOrderStatus Status { get; set; }
    public DateTime OpenedAtUtc { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public List<SalesOrderLineDto> Lines { get; set; } = [];
}

public sealed class SalesOrderLineDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public EProductType CompositionType { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string? Notes { get; set; }
    public List<SalesOrderLineExcludedIngredientDto> ExcludedIngredients { get; set; } = [];
}

public sealed class SalesOrderLineExcludedIngredientDto
{
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
}

public sealed class AddSalesOrderLineDto
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; } = 1;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public List<Guid> ExcludedIngredientIds { get; set; } = [];
}

public sealed class ConfirmSalesOrderDto
{
    [MinLength(1)]
    public List<AddSalesOrderLineDto> Lines { get; set; } = [];
}

public sealed class TableServiceSummaryDto
{
    public Guid TableId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Zone { get; set; }
    public ETableStatus Status { get; set; }
    public int Capacity { get; set; }
    public Guid? OpenOrderId { get; set; }
    public string? OpenOrderNumber { get; set; }
    public decimal? OpenOrderTotal { get; set; }
}
