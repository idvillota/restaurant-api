namespace Restaurant.Domain.Enums;

public enum EProductType
{
    Prepared = 0,
    Resale = 1,
    Bundle = 2,

    /// <summary>Combo sold at one price. The waiter picks one product per topic.</summary>
    ChoiceCombo = 3,
}
