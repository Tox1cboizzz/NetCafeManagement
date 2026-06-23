using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Results;
using WalletService.Domain.Entities;
using WalletService.Infrastructure.Data;

namespace WalletService.Application.DTOs
{
    public record WalletDto(Guid Id, Guid UserId, decimal Balance, decimal TotalTopup, string MemberTier, decimal Discount);
    public record TransactionDto(Guid Id, string Type, decimal Amount, string Note, DateTime CreatedAt);
    public record TopUpRequest(Guid UserId, decimal Amount);
    public record DeductRequest(Guid UserId, decimal Amount, string Note);
    public record CreateWalletRequest(Guid UserId);
}

namespace WalletService.Application.Commands
{
    using WalletService.Application.DTOs;

    public record CreateWalletCommand(Guid UserId) : IRequest<Result<WalletDto>>;
    public record TopUpCommand(Guid UserId, decimal Amount) : IRequest<Result<WalletDto>>;
    public record DeductCommand(Guid UserId, decimal Amount, string Note) : IRequest<Result<WalletDto>>;

    public class CreateWalletCommandHandler : IRequestHandler<CreateWalletCommand, Result<WalletDto>>
    {
        private readonly WalletDbContext _context;
        public CreateWalletCommandHandler(WalletDbContext context) => _context = context;

        public async Task<Result<WalletDto>> Handle(CreateWalletCommand request, CancellationToken cancellationToken)
        {
            var exists = await _context.Wallets.AnyAsync(w => w.UserId == request.UserId, cancellationToken);
            if (exists) return Result<WalletDto>.Failure("Wallet already exists for this user");

            var wallet = Wallet.Create(request.UserId);
            _context.Wallets.Add(wallet);
            await _context.SaveChangesAsync(cancellationToken);
            return Result<WalletDto>.Success(ToDto(wallet));
        }
    }

    public class TopUpCommandHandler : IRequestHandler<TopUpCommand, Result<WalletDto>>
    {
        private readonly WalletDbContext _context;
        public TopUpCommandHandler(WalletDbContext context) => _context = context;

        public async Task<Result<WalletDto>> Handle(TopUpCommand request, CancellationToken cancellationToken)
        {
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == request.UserId, cancellationToken);
            if (wallet == null) return Result<WalletDto>.Failure("Wallet not found");

            wallet.TopUp(request.Amount);
            _context.Transactions.Add(Transaction.Create(wallet.Id, TransactionType.Topup, request.Amount, "Top up"));
            await _context.SaveChangesAsync(cancellationToken);
            return Result<WalletDto>.Success(ToDto(wallet));
        }
    }

    public class DeductCommandHandler : IRequestHandler<DeductCommand, Result<WalletDto>>
    {
        private readonly WalletDbContext _context;
        public DeductCommandHandler(WalletDbContext context) => _context = context;

        public async Task<Result<WalletDto>> Handle(DeductCommand request, CancellationToken cancellationToken)
        {
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == request.UserId, cancellationToken);
            if (wallet == null) return Result<WalletDto>.Failure("Wallet not found");
            if (!wallet.Deduct(request.Amount)) return Result<WalletDto>.Failure("Insufficient balance");

            _context.Transactions.Add(Transaction.Create(wallet.Id, TransactionType.Deduct, request.Amount, request.Note));
            await _context.SaveChangesAsync(cancellationToken);
            return Result<WalletDto>.Success(ToDto(wallet));
        }
    }

    internal static class WalletMapper
    {
        public static WalletDto ToDto(Wallet w) =>
            new(w.Id, w.UserId, w.Balance, w.TotalTopup, w.MemberTier.ToString(), w.Discount);
    }
}

namespace WalletService.Application.Queries
{
    using WalletService.Application.DTOs;
    using static WalletService.Application.Commands.WalletMapper;

    public record GetWalletByUserIdQuery(Guid UserId) : IRequest<Result<WalletDto>>;
    public record GetTransactionsQuery(Guid UserId) : IRequest<Result<List<TransactionDto>>>;

    public class GetWalletByUserIdQueryHandler : IRequestHandler<GetWalletByUserIdQuery, Result<WalletDto>>
    {
        private readonly WalletDbContext _context;
        public GetWalletByUserIdQueryHandler(WalletDbContext context) => _context = context;

        public async Task<Result<WalletDto>> Handle(GetWalletByUserIdQuery request, CancellationToken cancellationToken)
        {
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == request.UserId, cancellationToken);
            if (wallet == null) return Result<WalletDto>.Failure("Wallet not found");
            return Result<WalletDto>.Success(ToDto(wallet));
        }
    }

    public class GetTransactionsQueryHandler : IRequestHandler<GetTransactionsQuery, Result<List<TransactionDto>>>
    {
        private readonly WalletDbContext _context;
        public GetTransactionsQueryHandler(WalletDbContext context) => _context = context;

        public async Task<Result<List<TransactionDto>>> Handle(GetTransactionsQuery request, CancellationToken cancellationToken)
        {
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == request.UserId, cancellationToken);
            if (wallet == null) return Result<List<TransactionDto>>.Failure("Wallet not found");

            var txs = await _context.Transactions
                .Where(t => t.WalletId == wallet.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new TransactionDto(t.Id, t.Type.ToString(), t.Amount, t.Note, t.CreatedAt))
                .ToListAsync(cancellationToken);
            return Result<List<TransactionDto>>.Success(txs);
        }
    }
}
