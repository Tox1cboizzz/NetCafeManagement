using Microsoft.EntityFrameworkCore;
using ReportService.Domain.Entities;

namespace ReportService.Infrastructure.Data;

public class ReportDbContext : DbContext
{
    public ReportDbContext(DbContextOptions<ReportDbContext> options) : base(options) { }
    public DbSet<DailyReport> DailyReports => Set<DailyReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DailyReport>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TotalRevenue).HasPrecision(18, 2);
            e.Property(x => x.PlayRevenue).HasPrecision(18, 2);
            e.Property(x => x.FoodRevenue).HasPrecision(18, 2);
            e.HasIndex(x => x.Date).IsUnique();
        });
    }
}
