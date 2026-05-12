namespace Restaurant.Domain.Common;

public abstract class EntityBase
{
    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
