using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Results;
using AuthService.Application.DTOs;
using AuthService.Infrastructure.Data;

namespace AuthService.Application.Commands;

public record BanUserCommand(Guid UserId) : IRequest<Result<UserDto>>;
public record UnbanUserCommand(Guid UserId) : IRequest<Result<UserDto>>;

public class BanUserCommandHandler : IRequestHandler<BanUserCommand, Result<UserDto>>
{
    private readonly AuthDbContext _context;
    public BanUserCommandHandler(AuthDbContext context) => _context = context;

    public async Task<Result<UserDto>> Handle(BanUserCommand req, CancellationToken ct)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == req.UserId && !u.IsDeleted, ct);
        if (user == null) return Result<UserDto>.Failure("User not found");
        if (user.Role.ToString() == "Admin") return Result<UserDto>.Failure("Không thể ban Admin");
        user.Ban();
        await _context.SaveChangesAsync(ct);
        return Result<UserDto>.Success(new UserDto(user.Id, user.Username, user.FullName, user.Phone, user.Role.ToString(), user.Status.ToString()));
    }
}

public class UnbanUserCommandHandler : IRequestHandler<UnbanUserCommand, Result<UserDto>>
{
    private readonly AuthDbContext _context;
    public UnbanUserCommandHandler(AuthDbContext context) => _context = context;

    public async Task<Result<UserDto>> Handle(UnbanUserCommand req, CancellationToken ct)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == req.UserId && !u.IsDeleted, ct);
        if (user == null) return Result<UserDto>.Failure("User not found");
        user.Activate();
        await _context.SaveChangesAsync(ct);
        return Result<UserDto>.Success(new UserDto(user.Id, user.Username, user.FullName, user.Phone, user.Role.ToString(), user.Status.ToString()));
    }
}
