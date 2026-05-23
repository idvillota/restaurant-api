using System.ComponentModel.DataAnnotations;

namespace Restaurant.Application.Features.Inventory;

public sealed class StockCheckLineDto
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; }

    public List<Guid> ExcludedIngredientIds { get; set; } = [];
}

public sealed class StockAvailabilityCheckDto
{
    /// <summary>Lines to evaluate (e.g. staging cart or new kitchen batch).</summary>
    [MinLength(1)]
    public List<StockCheckLineDto> Lines { get; set; } = [];

    /// <summary>When confirming a batch, include demand from all open orders plus these lines.</summary>
    public Guid? SalesOrderId { get; set; }
}

public sealed class IngredientShortageDto
{
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public decimal Required { get; set; }
    public decimal Available { get; set; }
    public decimal Missing { get; set; }
}

public sealed class StockAvailabilityResultDto
{
    public bool IsAvailable { get; set; }
    public List<IngredientShortageDto> Shortages { get; set; } = [];
}
