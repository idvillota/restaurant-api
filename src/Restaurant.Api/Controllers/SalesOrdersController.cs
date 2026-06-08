using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Authorization;
using Restaurant.Application.Authorization;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Sales.SalesOrders;

namespace Restaurant.Api.Controllers;

[ApiController]
[RequireFeature(FeatureCodes.ServiceSalon)]
[Route("api/[controller]")]
public sealed class SalesOrdersController : ControllerBase
{
    private readonly ISalesOrderService _service;

    public SalesOrdersController(ISalesOrderService service) => _service = service;

    [HttpGet("tables")]
    public async Task<ActionResult<IReadOnlyList<TableServiceSummaryDto>>> ListTables(
        CancellationToken cancellationToken = default) =>
        Ok(await _service.ListTableSummariesAsync(cancellationToken));

    [HttpGet("table/{tableId:guid}/open")]
    public async Task<ActionResult<SalesOrderDto>> GetOpenByTable(
        Guid tableId,
        CancellationToken cancellationToken = default)
    {
        var order = await _service.GetOpenByTableIdAsync(tableId, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("table/{tableId:guid}")]
    public async Task<ActionResult<SalesOrderDto>> StartForTable(
        Guid tableId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var created = await _service.StartOrderForTableAsync(tableId, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SalesOrderDto>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<ConfirmSalesOrderResultDto>> ConfirmOrder(
        Guid id,
        [FromBody] ConfirmSalesOrderDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var result = await _service.ConfirmOrderAsync(id, dto, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("{orderId:guid}/lines/{lineId:guid}")]
    public async Task<ActionResult<SalesOrderDto>> RemovePendingLine(
        Guid orderId,
        Guid lineId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var updated = await _service.RemovePendingLineAsync(orderId, lineId, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPatch("{orderId:guid}/lines/{lineId:guid}/quantity")]
    public async Task<ActionResult<SalesOrderDto>> UpdatePendingLineQuantity(
        Guid orderId,
        Guid lineId,
        [FromBody] UpdatePendingLineQuantityDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var updated = await _service.UpdatePendingLineQuantityAsync(
                orderId,
                lineId,
                dto.Quantity,
                cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/lines")]
    public async Task<ActionResult<SalesOrderDto>> AddLine(
        Guid id,
        [FromBody] AddSalesOrderLineDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var updated = await _service.AddLineAsync(id, dto, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<SalesOrderDto>> Complete(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var updated = await _service.CompleteAsync(id, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
