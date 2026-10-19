namespace Restaurant.Domain.Enums;

/// <summary>Subtag of the Bre-B key inside the EMV merchant account template.</summary>
public enum BreBKeyType
{
    Identification = 1,
    Phone = 2,
    Email = 3,
    Alphanumeric = 4,
    Merchant = 5,
}
