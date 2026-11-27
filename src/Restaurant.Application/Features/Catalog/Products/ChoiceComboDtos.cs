using System.ComponentModel.DataAnnotations;

namespace Restaurant.Application.Features.Catalog.Products;

public sealed class ChoiceComboDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public IReadOnlyList<ChoiceComboGroupDto> Groups { get; set; } = [];
}

public sealed class ChoiceComboGroupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public IReadOnlyList<ChoiceComboOptionDto> Options { get; set; } = [];
}

public sealed class ChoiceComboOptionDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class SetChoiceComboGroupDto
{
    [Required]
    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    [Required]
    public List<Guid> OptionProductIds { get; set; } = [];
}
