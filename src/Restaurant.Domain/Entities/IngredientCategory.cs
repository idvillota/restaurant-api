using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class IngredientCategory : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Ingredient> Ingredients { get; set; } = new List<Ingredient>();
}
