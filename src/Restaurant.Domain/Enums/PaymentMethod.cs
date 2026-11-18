namespace Restaurant.Domain.Enums;

public enum PaymentMethod
{
    Cash = 0,
    Card = 1,
    Transfer = 2,
    Other = 3,
    /// <summary>Redeem one or more prepaid lunch tickets. Not cash revenue.</summary>
    Tiquetera = 4,
}
