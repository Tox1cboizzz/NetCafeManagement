using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReportService.Application.DTOs;
using ReportService.Application.Queries;

namespace ReportService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ReportsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("daily")]
    public async Task<IActionResult> GetDaily([FromQuery] DateTime date)
    {
        var result = await _mediator.Send(new GetDailyReportQuery(date));
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    [HttpGet("range")]
    public async Task<IActionResult> GetRange([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var result = await _mediator.Send(new GetReportRangeQuery(from, to));
        return Ok(result.Data);
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateReportRequest req)
    {
        var result = await _mediator.Send(new GenerateDailyReportCommand(req.Date, req.PlayRevenue, req.FoodRevenue, req.TotalSessions));
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }
}
