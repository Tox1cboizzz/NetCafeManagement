using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FoodService.Application.Commands;
using FoodService.Application.DTOs;

namespace FoodService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FoodController : ControllerBase
{
    private readonly IMediator _mediator;
    public FoodController(IMediator mediator) => _mediator = mediator;

    [HttpGet("menu")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMenu()
    {
        var result = await _mediator.Send(new GetMenuQuery());
        return Ok(result.Data);
    }

    [HttpPost("menu")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateMenuItem([FromBody] CreateMenuItemRequest req)
    {
        var result = await _mediator.Send(new CreateMenuItemCommand(req.Name, req.Category, req.Price));
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpPost("orders")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest req)
    {
        var result = await _mediator.Send(new CreateOrderCommand(req.SessionId, req.ItemId, req.Quantity));
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpGet("orders/session/{sessionId:guid}")]
    public async Task<IActionResult> GetOrdersBySession(Guid sessionId)
    {
        var result = await _mediator.Send(new GetOrdersBySessionQuery(sessionId));
        return Ok(result.Data);
    }
}
