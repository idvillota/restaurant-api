namespace Restaurant.Domain.Enums;

/// <summary>
/// How the tenant tracks sellable availability.
/// </summary>
public enum StockControlMode
{
    /// <summary>Ingredient recipes, purchases update ingredient stock (legacy InventoryEnabled=true).</summary>
    Ingredients = 0,

    /// <summary>No stock checks or deductions (legacy InventoryEnabled=false).</summary>
    None = 1,

    /// <summary>Finished-goods units on products; increased via production documents, decreased on sale.</summary>
    ProductUnits = 2,
}
