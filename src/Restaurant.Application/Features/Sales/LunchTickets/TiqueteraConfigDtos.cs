using System.ComponentModel.DataAnnotations;
using Restaurant.Domain.Enums;

namespace Restaurant.Application.Features.Sales.LunchTickets;

public sealed class TiqueteraConfigDto
{
    public bool TiqueterasEnabled { get; set; }
    public IReadOnlyList<TiqueteraPackDto> Packs { get; set; } = [];
    public IReadOnlyList<TiqueteraRedeemableProductDto> RedeemableProducts { get; set; } = [];
}

public sealed class TiqueteraPackDto
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid ProductTypeId { get; set; }
    public string ProductTypeName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int LunchTicketPackSize { get; set; }
    public bool IsActive { get; set; }
}

public sealed class TiqueteraRedeemableProductDto
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
}

public sealed class CreateTiqueteraPackDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid ProductTypeId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [Range(1, 365)]
    public int LunchTicketPackSize { get; set; }
}

public sealed class UpdateTiqueteraPackDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid ProductTypeId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [Range(1, 365)]
    public int LunchTicketPackSize { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class SetTiqueteraRedeemableProductsDto
{
    public List<Guid> ProductIds { get; set; } = [];
}

public sealed class SellTiqueteraPackDto
{
    [Required]
    public Guid PackProductId { get; set; }

    [Required]
    public Guid CustomerId { get; set; }

    [Range(1, 50)]
    public int Quantity { get; set; } = 1;

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [MaxLength(120)]
    public string? ExternalReference { get; set; }
}

public sealed class SellTiqueteraPackResultDto
{
    public Guid BillId { get; set; }
    public string BillNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PackProductName { get; set; } = string.Empty;
    public int TicketsGranted { get; set; }
    public int RemainingTickets { get; set; }
    public decimal TotalPaid { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
}
