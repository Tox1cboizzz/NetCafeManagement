using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Results;
using SessionService.Domain.Entities;
using SessionService.Infrastructure.Data;

// DTOs
namespace SessionService.Application.DTOs;
public record SessionDto(Guid Id, Guid UserId, Guid MachineId, DateTime StartTime, DateTime? EndTime, decimal PricePerHour, decimal Discount, decimal TotalCost, string Status);
public record InvoiceDto(Guid Id, Guid SessionId, decimal PlayCost, decimal FoodCost, decimal TotalCost, DateTime CreatedAt);
public record StartSessionRequest(Guid UserId, Guid MachineId, decimal PricePerHour, decimal Discount);
public record CloseSessionRequest(decimal FoodCost);

// Commands
namespace SessionService.Application.Commands;
using SessionService.Application.DTOs;

public record StartSessionCommand(Guid UserId, Guid MachineId, decimal PricePerHour, decimal Discount) : IRequest<Result<SessionDto>>;
public record CloseSessionCommand(Guid SessionId, decimal FoodCost) : IRequest<Result<InvoiceDto>>;
public record GetActiveSessionQuery(Guid UserId) : IRequest<Result<SessionDto>>;
public record GetInvoiceQuery(Guid SessionId) : IRequest<Result<InvoiceDto>>;

public class StartSessionCommandHandler : IRequestHandler<StartSessionCommand, Result<SessionDto>>
{
    private readonly SessionDbContext _context;
    public StartSessionCommandHandler(SessionDbContext ctx) => _context = ctx;

    public async Task<Result<SessionDto>> Handle(StartSessionCommand req, CancellationToken ct)
    {
        var active = await _context.Sessions.AnyAsync(s => s.UserId == req.UserId && s.Status == SessionStatus.Active, ct);
        if (active) return Result<SessionDto>.Failure("User already has an active session");

        var session = Session.Create(req.UserId, req.MachineId, req.PricePerHour, req.Discount);
        _context.Sessions.Add(session);
        await _context.SaveChangesAsync(ct);
        return Result<SessionDto>.Success(ToDto(session));
    }
}

public class CloseSessionCommandHandler : IRequestHandler<CloseSessionCommand, Result<InvoiceDto>>
{
    private readonly SessionDbContext _context;
    public CloseSessionCommandHandler(SessionDbContext ctx) => _context = ctx;

    public async Task<Result<InvoiceDto>> Handle(CloseSessionCommand req, CancellationToken ct)
    {
        var session = await _context.Sessions.FindAsync(req.SessionId);
        if (session == null) return Result<InvoiceDto>.Failure("Session not found");
        if (session.Status == SessionStatus.Closed) return Result<InvoiceDto>.Failure("Session already closed");

        session.Close();
        var invoice = Invoice.Create(session.Id, session.TotalCost, req.FoodCost);
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(ct);
        return Result<InvoiceDto>.Success(new InvoiceDto(invoice.Id, invoice.SessionId, invoice.PlayCost, invoice.FoodCost, invoice.TotalCost, invoice.CreatedAt));
    }
}

public class GetActiveSessionQueryHandler : IRequestHandler<GetActiveSessionQuery, Result<SessionDto>>
{
    private readonly SessionDbContext _context;
    public GetActiveSessionQueryHandler(SessionDbContext ctx) => _context = ctx;

    public async Task<Result<SessionDto>> Handle(GetActiveSessionQuery req, CancellationToken ct)
    {
        var session = await _context.Sessions.FirstOrDefaultAsync(s => s.UserId == req.UserId && s.Status == SessionStatus.Active, ct);
        return session == null ? Result<SessionDto>.Failure("No active session") : Result<SessionDto>.Success(ToDto(session));
    }
}

public class GetInvoiceQueryHandler : IRequestHandler<GetInvoiceQuery, Result<InvoiceDto>>
{
    private readonly SessionDbContext _context;
    public GetInvoiceQueryHandler(SessionDbContext ctx) => _context = ctx;

    public async Task<Result<InvoiceDto>> Handle(GetInvoiceQuery req, CancellationToken ct)
    {
        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.SessionId == req.SessionId, ct);
        return invoice == null ? Result<InvoiceDto>.Failure("Invoice not found") : Result<InvoiceDto>.Success(new InvoiceDto(invoice.Id, invoice.SessionId, invoice.PlayCost, invoice.FoodCost, invoice.TotalCost, invoice.CreatedAt));
    }
}

file static SessionDto ToDto(Session s) =>
    new(s.Id, s.UserId, s.MachineId, s.StartTime, s.EndTime, s.PricePerHour, s.Discount, s.TotalCost, s.Status.ToString());
