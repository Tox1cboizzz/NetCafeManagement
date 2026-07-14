using SharedKernel.BaseEntities;

namespace MachineService.Domain.Entities;

file static class VnTime
{
    private static readonly TimeZoneInfo VnZone =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnZone);
}

public class Machine : BaseEntity
{
    public string Name { get; private set; } = null!;
    public MachineType Type { get; private set; }
    public decimal PricePerHour { get; private set; }
    public MachineStatus Status { get; private set; }
    public Guid? CurrentUserId { get; private set; }
    public string? CurrentUserPhone { get; private set; }
    public DateTime? SessionStartTime { get; private set; }
    public string? Specs { get; private set; }

    private Machine() { }

    public static Machine Create(string name, MachineType type, decimal pricePerHour, string? specs = null)
        => new() { Name = name, Type = type, PricePerHour = pricePerHour, Status = MachineStatus.Available, Specs = specs };

    public void Occupy(Guid userId, string userPhone)
    {
        CurrentUserId = userId;
        CurrentUserPhone = userPhone;
        SessionStartTime = VnTime.Now;
        Status = MachineStatus.InUse;
        SetUpdated();
    }

    public void Release()
    {
        CurrentUserId = null;
        CurrentUserPhone = null;
        SessionStartTime = null;
        Status = MachineStatus.Available;
        SetUpdated();
    }

    public void SetMaintenance() { Status = MachineStatus.Maintenance; SetUpdated(); }

    public void UpdatePrice(decimal pricePerHour) { PricePerHour = pricePerHour; SetUpdated(); }

    public void Update(string name, decimal pricePerHour, string? specs)
    {
        Name = name;
        PricePerHour = pricePerHour;
        Specs = specs;
        SetUpdated();
    }
}

public enum MachineType { Normal = 1, Premium = 2 }
public enum MachineStatus { Available = 1, InUse = 2, Maintenance = 3 }
