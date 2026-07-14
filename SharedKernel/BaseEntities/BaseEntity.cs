namespace SharedKernel.BaseEntities;

public abstract class BaseEntity
{
    private static readonly TimeZoneInfo VnZone =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
    private static DateTime VnNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnZone);

    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = VnNow;
    public DateTime? UpdatedAt { get; protected set; }
    public bool IsDeleted { get; protected set; } = false;

    public void SoftDelete() => IsDeleted = true;
    public void SetUpdated() => UpdatedAt = VnNow;
}
