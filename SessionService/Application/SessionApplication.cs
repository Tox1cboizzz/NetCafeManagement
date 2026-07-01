using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Results;
using SessionService.Domain.Entities;
using SessionService.Infrastructure.Data;

namespace SessionService.Application.DTOs
{
    public record SessionDto(Guid Id, Guid UserId, Guid MachineId, DateTime StartTime, DateTime? EndTime, decimal PricePerHour, decimal Discount, decimal TotalCost, string Status);
    public record InvoiceDto(Guid Id, Guid SessionId, decimal PlayCost, decimal FoodCost, decimal TotalCost, DateTime CreatedAt);
    public record RevenueDto(decimal TotalRevenue, decimal ActiveRevenue, decimal ClosedRevenue, int TotalSessions, int ActiveSessions);
    public record StartSessionRequest(Guid UserId, Guid MachineId, decimal PricePerHour, decimal Discount);
    public record CloseSessionRequest(decimal FoodCost);
}

namespace SessionService.Application.Commands
{
    using SessionService.Application.DTOs;

    public record StartSessionCommand(Guid UserId, Guid MachineId, decimal PricePerHour, decimal Discount) : IRequest<Result<SessionDto>>;
    public record CloseSessionCommand(Guid SessionId, decimal FoodCost) : IRequest<Result<InvoiceDto>>;
    public record GetActiveSessionQuery(Guid UserId) : IRequest<Result<SessionDto>>;
    public record GetAllSessionsQuery(DateTime? Date) : IRequest<Result<List<SessionDto>>>;
    public record GetInvoiceQuery(Guid SessionId) : IRequest<Result<InvoiceDto>>;
    public record GetRevenueQuery(DateTime? Date) : IRequest<Result<RevenueDto>>;

    internal static class SessionMapper
    {
        public static SessionDto ToDto(Session s) =>
            new(s.Id, s.UserId, s.MachineId, s.StartTime, s.EndTime, s.PricePerHour, s.Discount, s.TotalCost, s.Status.ToString());
    }

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
            return Result<SessionDto>.Success(SessionMapper.ToDto(session));
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
            if (session.Status == SessionStatus.Closed) return Result<InvoiceDto>.Failure("Already closed");

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
            return session == null ? Result<SessionDto>.Failure("No active session") : Result<SessionDto>.Success(SessionMapper.ToDto(session));
        }
    }

    public class GetAllSessionsQueryHandler : IRequestHandler<GetAllSessionsQuery, Result<List<SessionDto>>>
    {
        private readonly SessionDbContext _context;
        public GetAllSessionsQueryHandler(SessionDbContext ctx) => _context = ctx;
        public async Task<Result<List<SessionDto>>> Handle(GetAllSessionsQuery req, CancellationToken ct)
        {
            var query = _context.Sessions.AsQueryable();
            if (req.Date.HasValue)
                query = query.Where(s => s.StartTime.Date == req.Date.Value.Date);
            else
                query = query.Where(s => s.StartTime.Date == DateTime.UtcNow.Date);

            var sessions = await query.OrderByDescending(s => s.StartTime).ToListAsync(ct);
            return Result<List<SessionDto>>.Success(sessions.Select(SessionMapper.ToDto).ToList());
        }
    }

    public class GetRevenueQueryHandler : IRequestHandler<GetRevenueQuery, Result<RevenueDto>>
    {
        private readonly SessionDbContext _context;
        public GetRevenueQueryHandler(SessionDbContext ctx) => _context = ctx;
        public async Task<Result<RevenueDto>> Handle(GetRevenueQuery req, CancellationToken ct)
        {
            var date = req.Date ?? DateTime.UtcNow;
            var sessions = await _context.Sessions
                .Where(s => s.StartTime.Date == date.Date)
                .ToListAsync(ct);

            // Active sessions: tính tiền đã chơi đến hiện tại (realtime)
            var now = DateTime.UtcNow;
            var activeRevenue = sessions
                .Where(s => s.Status == SessionStatus.Active)
                .Sum(s => s.TotalCost); // TotalCost đã được cộng dồn từ background job

            var closedRevenue = sessions
                .Where(s => s.Status == SessionStatus.Closed)
                .Sum(s => s.TotalCost);

            return Result<RevenueDto>.Success(new RevenueDto(
                activeRevenue + closedRevenue,
                activeRevenue,
                closedRevenue,
                sessions.Count,
                sessions.Count(s => s.Status == SessionStatus.Active)
            ));
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
}
