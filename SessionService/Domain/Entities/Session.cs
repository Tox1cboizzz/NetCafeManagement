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
        => new()
        {
            UserId = userId,
            MachineId = machineId,
            StartTime = DateTime.UtcNow,
            PricePerHour = pricePerHour,
            Discount = discount,
            TotalCost = 0,
            Status = SessionStatus.Active
        };

    /// <summary>
    /// Cộng dồn tiền đã trừ thực tế (gọi mỗi giây từ background job).
    /// TotalCost là nguồn sự thật duy nhất, không tính lại từ thời gian khi đóng phiên.
    /// </summary>
    public void AccumulateCost(decimal amount)
    {
        TotalCost += amount;
        SetUpdated();
    }

    /// <summary>
    /// Đóng phiên - chỉ chốt EndTime và Status.
    /// TotalCost giữ nguyên giá trị đã cộng dồn từ AccumulateCost.
    /// </summary>
    public void Close()
    {
        EndTime = DateTime.UtcNow;
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
