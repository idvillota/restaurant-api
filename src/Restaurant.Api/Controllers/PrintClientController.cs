using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Authorization;
using Restaurant.Application.Authorization;
using Restaurant.Application.Common.Interfaces;

namespace Restaurant.Api.Controllers;

[ApiController]
[RequireFeature(FeatureCodes.ServiceSalon)]
[Route("api/print-client/jobs")]
public sealed class PrintClientController : ControllerBase
{
    private readonly IPrintJobQueue _jobs;

    public PrintClientController(IPrintJobQueue jobs) => _jobs = jobs;

    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<PrintJobTicket>>> Pending(CancellationToken cancellationToken) =>
        Ok(await _jobs.ListPendingAsync(cancellationToken));

    [HttpPost("{id:guid}/ack")]
    public async Task<IActionResult> Ack(
        Guid id,
        [FromBody] PrintJobAckBody body,
        CancellationToken cancellationToken)
    {
        var found = await _jobs.AcknowledgeAsync(id, body.Success, body.ErrorMessage, cancellationToken);
        return found ? Ok() : NotFound();
    }
}

public sealed class PrintJobAckBody
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
