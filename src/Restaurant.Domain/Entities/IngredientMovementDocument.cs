using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class IngredientMovementDocument : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid IngredientMovementTypeId { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    public IngredientMovementType MovementType { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public ICollection<IngredientMovement> Lines { get; set; } = new List<IngredientMovement>();
}
