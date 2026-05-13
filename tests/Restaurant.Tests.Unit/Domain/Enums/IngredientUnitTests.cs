using Restaurant.Domain.Enums;

namespace Restaurant.Tests.Unit.Domain.Enums;

public sealed class IngredientUnitTests
{
    [Theory]
    [InlineData(IngredientUnit.Unit, 0)]
    [InlineData(IngredientUnit.Kilogram, 1)]
    [InlineData(IngredientUnit.Gram, 2)]
    [InlineData(IngredientUnit.Liter, 3)]
    [InlineData(IngredientUnit.Milliliter, 4)]
    public void IngredientUnit_has_expected_underlying_values(IngredientUnit unit, int expected) =>
        Assert.Equal(expected, (int)unit);
}
