using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Authorization;
using Restaurant.Application.Authorization;
using Restaurant.Application.Features.MobileSync;

namespace Restaurant.Api.Controllers;

[ApiController]
[RequireFeature(FeatureCodes.ServiceSalon)]
[Route("api/mobile-sync")]
public sealed class MobileSyncController : ControllerBase
{
    private readonly IMobileSyncService _sync;

    public MobileSyncController(IMobileSyncService sync) => _sync = sync;

    [HttpPost("devices")]
    public ActionResult Register([FromBody] RegisterMobileDeviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
            return BadRequest(new { message = "deviceId es obligatorio." });

        return Ok(new { });
    }

    [HttpPost("batches")]
    public async Task<ActionResult<MobileSyncBatchResponse>> Upload(
        [FromBody] MobileSyncBatchRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
            return BadRequest(new { message = "deviceId es obligatorio." });

        return Ok(await _sync.UploadBatchAsync(request, cancellationToken));
    }

    [HttpGet("pending-sales")]
    public async Task<ActionResult<PendingMobileSalesResponse>> Pending(
        [FromQuery] string deviceId,
        [FromQuery] string? since,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return BadRequest(new { message = "deviceId es obligatorio." });

        return Ok(await _sync.GetPendingSalesAsync(deviceId, since, cancellationToken));
    }

    [HttpPost("payments")]
    public async Task<ActionResult<MobileSyncPaymentResponse>> Pay(
        [FromBody] MobileSyncPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
            return BadRequest(new { message = "deviceId es obligatorio." });
        if (request.RemoteIds.Count == 0)
            return BadRequest(new { message = "La cuenta no tiene pedidos." });

        return Ok(await _sync.PayAsync(request, cancellationToken));
    }
}
