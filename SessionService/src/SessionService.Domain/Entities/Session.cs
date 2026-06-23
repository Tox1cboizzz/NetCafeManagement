using SharedKernel.BaseEntities;

namespace SessionService.Domain.Entities;

public class Session : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid MachineId { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public decimal PricePerHour { get; private set; }
    public decimal Discount { get; private set; }
    public decimal TotalCost { get; private set; }
    public SessionStatus Status { get; private set; }

    private Session() { }

    public static Session Create(Guid userId, Guid machineId, decimal pricePerHour, decimal discount)
        => new() { UserId = userId, MachineId = machineId, StartTime = DateTime.UtcNow, PricePerHour = pricePerHour, Discount = discount, Status = SessionStatus.Active };

    public void Close()
    {
        EndTime = DateTime.UtcNow;
        var hours = (decimal)(EndTime.Value - StartTime).TotalHours;
        var rawCost = hours * PricePerHour;
        TotalCost = rawCost * (1 - Discount);
        Status = SessionStatus.Closed;
        SetUpdated();
    }
}

public class Invoice : BaseEntity
{
    public Guid SessionId { get; private set; }
    public decimal PlayCost { get; private set; }
    public decimal FoodCost { get; private set; }
    public decimal TotalCost { get; private set; }

    private Invoice() { }

    public static Invoice Create(Guid sessionId, decimal playCost, decimal foodCost)
        => new() { SessionId = sessionId, PlayCost = playCost, FoodCost = foodCost, TotalCost = playCost + foodCost };
}

public enum SessionStatus { Active = 1, Closed = 2 }
