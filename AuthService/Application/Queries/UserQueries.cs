using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Results;
using AuthService.Application.DTOs;
using AuthService.Infrastructure.Data;

namespace AuthService.Application.Queries;

public record GetAllUsersQuery : IRequest<Result<List<UserDto>>>;
public record GetUserByIdQuery(Guid Id) : IRequest<Result<UserDto>>;

public class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, Result<List<UserDto>>>
{
    private readonly AuthDbContext _context;
    public GetAllUsersQueryHandler(AuthDbContext context) => _context = context;

    public async Task<Result<List<UserDto>>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _context.Users
            .Where(u => !u.IsDeleted)
            .Select(u => new UserDto(u.Id, u.Username, u.FullName, u.Phone, u.Role.ToString(), u.Status.ToString()))
            .ToListAsync(cancellationToken);

        return Result<List<UserDto>>.Success(users);
    }
}

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, Result<UserDto>>
{
    private readonly AuthDbContext _context;
    public GetUserByIdQueryHandler(AuthDbContext context) => _context = context;

    public async Task<Result<UserDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.Id && !u.IsDeleted, cancellationToken);
        if (user == null) return Result<UserDto>.Failure("User not found");

        return Result<UserDto>.Success(new UserDto(user.Id, user.Username, user.FullName, user.Phone, user.Role.ToString(), user.Status.ToString()));
    }
}
