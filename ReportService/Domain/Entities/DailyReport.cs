using SharedKernel.BaseEntities;

namespace ReportService.Domain.Entities;

public class DailyReport : BaseEntity
{
    public DateTime Date { get; private set; }
    public decimal TotalRevenue { get; private set; }
    public decimal PlayRevenue { get; private set; }
    public decimal FoodRevenue { get; private set; }
    public int TotalSessions { get; private set; }

    private DailyReport() { }

    public static DailyReport Create(DateTime date, decimal playRevenue, decimal foodRevenue, int totalSessions)
        => new()
        {
            Date = date.Date,
            PlayRevenue = playRevenue,
            FoodRevenue = foodRevenue,
            TotalRevenue = playRevenue + foodRevenue,
            TotalSessions = totalSessions
        };
}
