using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Results;
using ReportService.Domain.Entities;
using ReportService.Infrastructure.Data;

namespace ReportService.Application.DTOs
{
    public record DailyReportDto(Guid Id, DateTime Date, decimal TotalRevenue, decimal PlayRevenue, decimal FoodRevenue, int TotalSessions);
    public record GenerateReportRequest(DateTime Date, decimal PlayRevenue, decimal FoodRevenue, int TotalSessions);
}

namespace ReportService.Application.Queries
{
    using ReportService.Application.DTOs;

    public record GetDailyReportQuery(DateTime Date) : IRequest<Result<DailyReportDto>>;
    public record GetReportRangeQuery(DateTime From, DateTime To) : IRequest<Result<List<DailyReportDto>>>;
    public record GenerateDailyReportCommand(DateTime Date, decimal PlayRevenue, decimal FoodRevenue, int TotalSessions) : IRequest<Result<DailyReportDto>>;

    internal static class ReportMapper
    {
        public static DailyReportDto ToDto(DailyReport r) =>
            new(r.Id, r.Date, r.TotalRevenue, r.PlayRevenue, r.FoodRevenue, r.TotalSessions);
    }

    public class GenerateDailyReportCommandHandler : IRequestHandler<GenerateDailyReportCommand, Result<DailyReportDto>>
    {
        private readonly ReportDbContext _context;
        public GenerateDailyReportCommandHandler(ReportDbContext ctx) => _context = ctx;

        public async Task<Result<DailyReportDto>> Handle(GenerateDailyReportCommand req, CancellationToken ct)
        {
            var existing = await _context.DailyReports.FirstOrDefaultAsync(r => r.Date == req.Date.Date, ct);
            if (existing != null) return Result<DailyReportDto>.Failure("Report for this date already exists");

            var report = DailyReport.Create(req.Date, req.PlayRevenue, req.FoodRevenue, req.TotalSessions);
            _context.DailyReports.Add(report);
            await _context.SaveChangesAsync(ct);
            return Result<DailyReportDto>.Success(ReportMapper.ToDto(report));
        }
    }

    public class GetDailyReportQueryHandler : IRequestHandler<GetDailyReportQuery, Result<DailyReportDto>>
    {
        private readonly ReportDbContext _context;
        public GetDailyReportQueryHandler(ReportDbContext ctx) => _context = ctx;

        public async Task<Result<DailyReportDto>> Handle(GetDailyReportQuery req, CancellationToken ct)
        {
            var report = await _context.DailyReports.FirstOrDefaultAsync(r => r.Date == req.Date.Date, ct);
            return report == null
                ? Result<DailyReportDto>.Failure("Report not found")
                : Result<DailyReportDto>.Success(ReportMapper.ToDto(report));
        }
    }

    public class GetReportRangeQueryHandler : IRequestHandler<GetReportRangeQuery, Result<List<DailyReportDto>>>
    {
        private readonly ReportDbContext _context;
        public GetReportRangeQueryHandler(ReportDbContext ctx) => _context = ctx;

        public async Task<Result<List<DailyReportDto>>> Handle(GetReportRangeQuery req, CancellationToken ct)
        {
            var reports = await _context.DailyReports
                .Where(r => r.Date >= req.From.Date && r.Date <= req.To.Date)
                .OrderBy(r => r.Date)
                .Select(r => ReportMapper.ToDto(r))
                .ToListAsync(ct);
            return Result<List<DailyReportDto>>.Success(reports);
        }
    }
}
