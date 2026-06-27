using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Restaurant.Application.Features.Dashboard;

public sealed class DashboardPanelDto
{
    [Required]
    [MaxLength(64)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string WidgetType { get; set; } = string.Empty;

    [Range(0, 11)]
    public int X { get; set; }

    [Range(0, 100)]
    public int Y { get; set; }

    [Range(1, 12)]
    public int W { get; set; }

    [Range(1, 12)]
    public int H { get; set; }

    public Dictionary<string, JsonElement>? Config { get; set; }
}

public sealed class DashboardLayoutDto
{
    public int Version { get; set; } = 1;

    [MinLength(0)]
    public List<DashboardPanelDto> Panels { get; set; } = [];
}

public sealed class DashboardWidgetDefinitionDto
{
    public string WidgetType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string RequiredPermission { get; set; } = string.Empty;
    public int DefaultWidth { get; set; }
    public int DefaultHeight { get; set; }
    public int MinWidth { get; set; }
    public int MinHeight { get; set; }
}
