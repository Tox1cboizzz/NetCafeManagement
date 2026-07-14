using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MachineService.Application.Commands;
using MachineService.Application.DTOs;

namespace MachineService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MachinesController : ControllerBase
{
    private readonly IMediator _mediator;
    public MachinesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _mediator.Send(new GetAllMachinesQuery());
        return Ok(result.Data);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetMachineByIdQuery(id));
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateMachineRequest req)
    {
        var result = await _mediator.Send(new CreateMachineCommand(req.Name, req.Type, req.PricePerHour, req.Specs));
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}/price")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdatePrice(Guid id, [FromBody] UpdatePriceRequest req)
    {
        var result = await _mediator.Send(new UpdateMachinePriceCommand(id, req.PricePerHour));
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/occupy")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> Occupy(Guid id, [FromBody] OccupyMachineRequest req)
    {
        var result = await _mediator.Send(new OccupyMachineCommand(id, req.UserId, req.UserPhone));
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/release")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> Release(Guid id)
    {
        var result = await _mediator.Send(new ReleaseMachineCommand(id));
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // Internal release - không cần auth, gọi từ SessionService
    [HttpPost("{id:guid}/internal-release")]
    [AllowAnonymous]
    public async Task<IActionResult> InternalRelease(Guid id)
    {
        var result = await _mediator.Send(new ReleaseMachineCommand(id));
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }
}
