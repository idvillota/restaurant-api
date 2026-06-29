using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Authorization;
using Restaurant.Application.Authorization;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Sales.Bills;

namespace Restaurant.Api.Controllers;

[ApiController]
[RequireFeature(FeatureCodes.SettingsTenant)]
[Route("api/tenant/settings")]
public sealed class TenantSettingsController : ControllerBase
{
    private readonly ITenantSettingsService _service;

    public TenantSettingsController(ITenantSettingsService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<TenantSettingsDto>> Get(CancellationToken cancellationToken = default) =>
        Ok(await _service.GetAsync(cancellationToken));

    [HttpPut]
    public async Task<ActionResult<TenantSettingsDto>> Update(
        [FromBody] UpdateTenantSettingsDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return Ok(await _service.UpdateAsync(dto, cancellationToken));
    }
}
