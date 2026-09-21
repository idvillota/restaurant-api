namespace Restaurant.Infrastructure.Persistence.Seeding;

public sealed record DefaultIngredientMovementTypeDefinition(
    string Name,
    string? Description,
    bool IsInput);

public static class DefaultIngredientMovementTypes
{
    public static readonly DefaultIngredientMovementTypeDefinition[] All =
    [
        new("Ingreso por regalo", "Stock recibido sin costo de compra", IsInput: true),
        new("Ajuste positivo", "Corrección por conteo físico (más stock)", IsInput: true),
        new("Salida por baja", "Descarte intencional de producto", IsInput: false),
        new("Salida por pérdida", "Merma o deterioro no planificado", IsInput: false),
        new("Ajuste negativo", "Corrección por conteo físico (menos stock)", IsInput: false),
    ];
}
