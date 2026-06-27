using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Authorization;
using Restaurant.Application.Authorization;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Dashboard;

namespace Restaurant.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;

    public DashboardController(IDashboardService service) => _service = service;

    [HttpGet("layout")]
    [RequireFeature(FeatureCodes.DashboardView)]
    public async Task<ActionResult<DashboardLayoutDto>> GetLayout(CancellationToken cancellationToken = default) =>
        Ok(await _service.GetLayoutAsync(cancellationToken));

    [HttpPut("layout")]
    [RequireFeature(FeatureCodes.DashboardConfigure)]
    public async Task<ActionResult<DashboardLayoutDto>> UpdateLayout(
        [FromBody] DashboardLayoutDto layout,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return Ok(await _service.UpdateLayoutAsync(layout, cancellationToken));
    }

    [HttpGet("catalog")]
    [RequireFeature(FeatureCodes.DashboardConfigure)]
    public ActionResult<IReadOnlyList<DashboardWidgetDefinitionDto>> GetCatalog() =>
        Ok(_service.GetCatalog());
}
