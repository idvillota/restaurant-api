using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class IngredientMovementType : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsInput { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<IngredientMovementDocument> Documents { get; set; } = new List<IngredientMovementDocument>();
}
