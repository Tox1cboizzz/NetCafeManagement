using Microsoft.EntityFrameworkCore;
using SessionService.Domain.Entities;

namespace SessionService.Infrastructure.Data;

public class SessionDbContext : DbContext
{
    public SessionDbContext(DbContextOptions<SessionDbContext> options) : base(options) { }
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Session>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.PricePerHour).HasPrecision(18, 2);
            e.Property(x => x.Discount).HasPrecision(5, 4);
            e.Property(x => x.TotalCost).HasPrecision(18, 2);
        });
        modelBuilder.Entity<Invoice>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.PlayCost).HasPrecision(18, 2);
            e.Property(x => x.FoodCost).HasPrecision(18, 2);
            e.Property(x => x.TotalCost).HasPrecision(18, 2);
        });
    }
}
