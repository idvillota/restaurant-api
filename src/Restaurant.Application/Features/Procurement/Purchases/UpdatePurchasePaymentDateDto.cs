using System.ComponentModel.DataAnnotations;

namespace Restaurant.Application.Features.Procurement.Purchases;

public sealed class UpdatePurchasePaymentDateDto
{
    [Required]
    public DateTime? PaymentDateUtc { get; set; }
}
