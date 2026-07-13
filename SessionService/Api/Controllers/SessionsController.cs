using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SessionService.Application.Commands;
using SessionService.Application.DTOs;

namespace SessionService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SessionsController : ControllerBase
{
    private readonly IMediator _mediator;
    public SessionsController(IMediator mediator) => _mediator = mediator;

    [HttpPost("start")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> Start([FromBody] StartSessionRequest req)
    {
        var result = await _mediator.Send(new StartSessionCommand(req.UserId, req.MachineId, req.PricePerHour, req.Discount));
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpPost("{sessionId:guid}/close")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> Close(Guid sessionId, [FromBody] CloseSessionRequest req)
    {
        var result = await _mediator.Send(new CloseSessionCommand(sessionId, req.FoodCost));
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpGet("active/{userId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetActive(Guid userId)
    {
        var result = await _mediator.Send(new GetActiveSessionQuery(userId));
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    [HttpGet("history")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> GetHistory([FromQuery] DateTime? date)
    {
        var result = await _mediator.Send(new GetAllSessionsQuery(date));
        return Ok(result.Data);
    }

    [HttpGet("revenue")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> GetRevenue([FromQuery] DateTime? date)
    {
        var result = await _mediator.Send(new GetRevenueQuery(date));
        return Ok(result.Data);
    }

    [HttpGet("{sessionId:guid}/invoice")]
    public async Task<IActionResult> GetInvoice(Guid sessionId)
    {
        var result = await _mediator.Send(new GetInvoiceQuery(sessionId));
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }
}
