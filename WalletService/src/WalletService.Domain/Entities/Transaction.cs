using SharedKernel.BaseEntities;

namespace WalletService.Domain.Entities;

public class Transaction : BaseEntity
{
    public Guid WalletId { get; private set; }
    public TransactionType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string Note { get; private set; } = null!;

    private Transaction() { }

    public static Transaction Create(Guid walletId, TransactionType type, decimal amount, string note)
    {
        return new Transaction { WalletId = walletId, Type = type, Amount = amount, Note = note };
    }
}

public enum TransactionType { Topup = 1, Deduct = 2 }
