namespace Restaurant.Infrastructure.Persistence.Seeding;

public sealed record DefaultIngredientMovementTypeDefinition(
    string Name,
    string? Description,
    bool IsInput,
    int SortOrder);

public static class DefaultIngredientMovementTypes
{
    public static readonly DefaultIngredientMovementTypeDefinition[] All =
    [
        new("Ingreso por regalo", "Stock recibido sin costo de compra", IsInput: true, SortOrder: 10),
        new("Ajuste positivo", "Corrección por conteo físico (más stock)", IsInput: true, SortOrder: 20),
        new("Salida por baja", "Descarte intencional de producto", IsInput: false, SortOrder: 30),
        new("Salida por pérdida", "Merma o deterioro no planificado", IsInput: false, SortOrder: 40),
        new("Ajuste negativo", "Corrección por conteo físico (menos stock)", IsInput: false, SortOrder: 50),
    ];
}
