using Microsoft.EntityFrameworkCore;
using FoodService.Domain.Entities;

namespace FoodService.Infrastructure.Data;

public class FoodDbContext : DbContext
{
    public FoodDbContext(DbContextOptions<FoodDbContext> options) : base(options) { }
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MenuItem>(e => { e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(100); e.Property(x => x.Price).HasPrecision(18, 2); });
        modelBuilder.Entity<Order>(e => { e.HasKey(x => x.Id); e.Property(x => x.UnitPrice).HasPrecision(18, 2); e.Property(x => x.TotalPrice).HasPrecision(18, 2); });
    }
}
