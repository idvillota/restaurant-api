using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Authorization;
using Restaurant.Application.Authorization;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Reports;

namespace Restaurant.Api.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class OperationalReportsController : ControllerBase
{
    private readonly IOperationalReportsService _reports;

    public OperationalReportsController(IOperationalReportsService reports) => _reports = reports;

    [HttpGet("sales")]
    [RequireFeature(FeatureCodes.ReportsSales)]
    [ProducesResponseType(typeof(SalesReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SalesReportDto>> GetSales(
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        [FromQuery] Guid? productId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _reports.GetSalesReportAsync(startDate, endDate, productId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("ingredients")]
    [RequireFeature(FeatureCodes.ReportsIngredients)]
    [ProducesResponseType(typeof(IngredientsReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<IngredientsReportDto>> GetIngredients(
        [FromQuery] string? name,
        CancellationToken cancellationToken) =>
        Ok(await _reports.GetIngredientsReportAsync(name, cancellationToken));

    [HttpGet("sales-by-date")]
    [RequireFeature(FeatureCodes.ReportsSalesByDate)]
    [ProducesResponseType(typeof(DailySummaryReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DailySummaryReportDto>> GetSalesByDate(
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _reports.GetDailySummaryReportAsync(startDate, endDate, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("purchases")]
    [RequireFeature(FeatureCodes.ReportsPurchases)]
    [ProducesResponseType(typeof(PurchasesReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PurchasesReportDto>> GetPurchases(
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        [FromQuery] Guid? ingredientId,
        [FromQuery] Guid? providerId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _reports.GetPurchasesReportAsync(
                startDate,
                endDate,
                ingredientId,
                providerId,
                cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

}
