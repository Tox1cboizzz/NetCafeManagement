using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletService.Application.Commands;
using WalletService.Application.DTOs;
using WalletService.Application.Queries;

namespace WalletService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WalletController : ControllerBase
{
    private readonly IMediator _mediator;
    public WalletController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateWallet([FromBody] CreateWalletRequest req)
    {
        var result = await _mediator.Send(new CreateWalletCommand(req.UserId));
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetWallet(Guid userId)
    {
        var result = await _mediator.Send(new GetWalletByUserIdQuery(userId));
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    [HttpPost("topup")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> TopUp([FromBody] TopUpRequest req)
    {
        var result = await _mediator.Send(new TopUpCommand(req.UserId, req.Amount));
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpPost("deduct")]
    public async Task<IActionResult> Deduct([FromBody] DeductRequest req)
    {
        var result = await _mediator.Send(new DeductCommand(req.UserId, req.Amount, req.Note));
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // Internal endpoint - không cần auth, chỉ dùng giữa các service nội bộ
    [HttpPost("internal/deduct")]
    [AllowAnonymous]
    public async Task<IActionResult> InternalDeduct([FromBody] DeductRequest req)
    {
        var result = await _mediator.Send(new DeductCommand(req.UserId, req.Amount, req.Note));
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpGet("internal/{userId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> InternalGetWallet(Guid userId)
    {
        var result = await _mediator.Send(new GetWalletByUserIdQuery(userId));
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    [HttpGet("{userId:guid}/transactions")]
    public async Task<IActionResult> GetTransactions(Guid userId)
    {
        var result = await _mediator.Send(new GetTransactionsQuery(userId));
        return Ok(result.Data);
    }
}
