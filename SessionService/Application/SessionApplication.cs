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
        private readonly IHttpClientFactory _httpClientFactory;

        public StartSessionCommandHandler(SessionDbContext ctx, IHttpClientFactory httpClientFactory)
        {
            _context = ctx;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<Result<SessionDto>> Handle(StartSessionCommand req, CancellationToken ct)
        {
            // Check phiên đang chạy
            var active = await _context.Sessions.AnyAsync(s => s.UserId == req.UserId && s.Status == SessionStatus.Active, ct);
            if (active) return Result<SessionDto>.Failure("Khách này đang có phiên chơi chưa đóng");

            // Check số dư ví - phải có ít nhất đủ tiền 1 phút
            var minBalance = Math.Round(req.PricePerHour / 60m * (1 - req.Discount), 0);
            try
            {
                var walletClient = _httpClientFactory.CreateClient("WalletService");
                var res = await walletClient.GetAsync($"/api/wallet/internal/{req.UserId}", ct);
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync(ct);
                    var doc = System.Text.Json.JsonDocument.Parse(json);
                    var balance = doc.RootElement.GetProperty("balance").GetDecimal();

                    if (balance <= 0)
                        return Result<SessionDto>.Failure("Tài khoản không có tiền. Vui lòng nạp tiền trước khi chơi");

                    if (balance < minBalance)
                        return Result<SessionDto>.Failure($"Số dư không đủ. Cần ít nhất {minBalance:N0}đ để bắt đầu (đủ 1 phút chơi)");
                }
            }
            catch
            {
                // Nếu không kết nối được WalletService thì vẫn cho chơi (tránh block hoàn toàn)
            }

            var session = Session.Create(req.UserId, req.MachineId, req.PricePerHour, req.Discount);
            _context.Sessions.Add(session);
            await _context.SaveChangesAsync(ct);
            return Result<SessionDto>.Success(SessionMapper.ToDto(session));
        }
    }

    public class CloseSessionCommandHandler : IRequestHandler<CloseSessionCommand, Result<InvoiceDto>>
    {
        private readonly SessionDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;

        public CloseSessionCommandHandler(SessionDbContext ctx, IHttpClientFactory httpClientFactory)
        {
            _context = ctx;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<Result<InvoiceDto>> Handle(CloseSessionCommand req, CancellationToken ct)
        {
            var session = await _context.Sessions.FindAsync(req.SessionId);
            if (session == null) return Result<InvoiceDto>.Failure("Session not found");
            if (session.Status == SessionStatus.Closed) return Result<InvoiceDto>.Failure("Already closed");

            var machineId = session.MachineId;
            session.Close();
            var invoice = Invoice.Create(session.Id, session.TotalCost, req.FoodCost);
            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync(ct);

            // Giải phóng máy sau khi đóng phiên
            try
            {
                var machineClient = _httpClientFactory.CreateClient("MachineService");
                await machineClient.PostAsync($"/api/machines/{machineId}/release", null, ct);
            }
            catch { }

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
            // Không filter ngày - lấy 50 phiên gần nhất để tránh lệch timezone
            var sessions = await query.OrderByDescending(s => s.StartTime).Take(50).ToListAsync(ct);
            return Result<List<SessionDto>>.Success(sessions.Select(SessionMapper.ToDto).ToList());
        }
    }

    public class GetRevenueQueryHandler : IRequestHandler<GetRevenueQuery, Result<RevenueDto>>
    {
        private readonly SessionDbContext _context;
        public GetRevenueQueryHandler(SessionDbContext ctx) => _context = ctx;
        public async Task<Result<RevenueDto>> Handle(GetRevenueQuery req, CancellationToken ct)
        {
            // Lấy tất cả sessions trong 24h gần nhất để tránh lệch timezone
            var cutoff = DateTime.UtcNow.AddHours(-24);
            var sessions = await _context.Sessions
                .Where(s => req.Date.HasValue
                    ? s.StartTime.Date == req.Date.Value.Date
                    : s.StartTime >= cutoff)
                .ToListAsync(ct);
            var activeRevenue = sessions.Where(s => s.Status == SessionStatus.Active).Sum(s => s.TotalCost);
            var closedRevenue = sessions.Where(s => s.Status == SessionStatus.Closed).Sum(s => s.TotalCost);
            return Result<RevenueDto>.Success(new RevenueDto(
                activeRevenue + closedRevenue, activeRevenue, closedRevenue,
                sessions.Count, sessions.Count(s => s.Status == SessionStatus.Active)));
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
