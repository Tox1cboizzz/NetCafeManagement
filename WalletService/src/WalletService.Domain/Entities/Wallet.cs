using SharedKernel.BaseEntities;

namespace WalletService.Domain.Entities;

public class Wallet : BaseEntity
{
    public Guid UserId { get; private set; }
    public decimal Balance { get; private set; }
    public decimal TotalTopup { get; private set; }
    public MemberTier MemberTier { get; private set; }
    public decimal Discount { get; private set; }

    private Wallet() { }

    public static Wallet Create(Guid userId)
    {
        return new Wallet
        {
            UserId = userId,
            Balance = 0,
            TotalTopup = 0,
            MemberTier = MemberTier.Normal,
            Discount = 0
        };
    }

    public void TopUp(decimal amount)
    {
        Balance += amount;
        TotalTopup += amount;
        UpdateTier();
        SetUpdated();
    }

    public bool Deduct(decimal amount)
    {
        if (Balance < amount) return false;
        Balance -= amount;
        SetUpdated();
        return true;
    }

    private void UpdateTier()
    {
        if (TotalTopup >= 10_000_000m && MemberTier == MemberTier.Normal)
        {
            MemberTier = MemberTier.VIP;
            Discount = 0.05m; // 5%
        }
    }
}

public enum MemberTier { Normal = 1, VIP = 2 }
