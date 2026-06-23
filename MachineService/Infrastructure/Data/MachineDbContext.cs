using Microsoft.EntityFrameworkCore;
using MachineService.Domain.Entities;

namespace MachineService.Infrastructure.Data;

public class MachineDbContext : DbContext
{
    public MachineDbContext(DbContextOptions<MachineDbContext> options) : base(options) { }
    public DbSet<Machine> Machines => Set<Machine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Machine>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.Property(x => x.PricePerHour).HasPrecision(18, 2);
            e.Property(x => x.Specs).HasMaxLength(200);
        });
    }
}
