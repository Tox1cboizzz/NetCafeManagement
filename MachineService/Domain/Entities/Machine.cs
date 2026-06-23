// ── Domain ────────────────────────────────────────────────────────────────────
using SharedKernel.BaseEntities;

namespace MachineService.Domain.Entities;

public class Machine : BaseEntity
{
    public string Name { get; private set; } = null!;
    public MachineType Type { get; private set; }
    public decimal PricePerHour { get; private set; }
    public MachineStatus Status { get; private set; }
    public Guid? CurrentUserId { get; private set; }
    public string? Specs { get; private set; }

    private Machine() { }

    public static Machine Create(string name, MachineType type, decimal pricePerHour, string? specs = null)
        => new() { Name = name, Type = type, PricePerHour = pricePerHour, Status = MachineStatus.Available, Specs = specs };

    public void Occupy(Guid userId) { CurrentUserId = userId; Status = MachineStatus.InUse; SetUpdated(); }
    public void Release() { CurrentUserId = null; Status = MachineStatus.Available; SetUpdated(); }
    public void SetMaintenance() { Status = MachineStatus.Maintenance; SetUpdated(); }
    public void Update(string name, decimal pricePerHour, string? specs) { Name = name; PricePerHour = pricePerHour; Specs = specs; SetUpdated(); }
}

public enum MachineType { Normal = 1, Premium = 2 }
public enum MachineStatus { Available = 1, InUse = 2, Maintenance = 3 }
