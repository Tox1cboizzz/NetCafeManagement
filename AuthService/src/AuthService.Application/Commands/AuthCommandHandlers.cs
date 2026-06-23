using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Results;
using AuthService.Application.DTOs;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using AuthService.Infrastructure.Data;
using AuthService.Infrastructure.Services;

namespace AuthService.Application.Commands;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<UserDto>>
{
    private readonly AuthDbContext _context;

    public RegisterCommandHandler(AuthDbContext context)
    {
        _context = context;
    }

    public async Task<Result<UserDto>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var exists = await _context.Users.AnyAsync(u => u.Username == request.Username, cancellationToken);
        if (exists)
            return Result<UserDto>.Failure("Username already exists");

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
            role = UserRole.Customer;

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var user = User.Create(request.Username, passwordHash, request.FullName, request.Phone, role);

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<UserDto>.Success(new UserDto(user.Id, user.Username, user.FullName, user.Phone, user.Role.ToString(), user.Status.ToString()));
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private readonly AuthDbContext _context;
    private readonly IJwtService _jwtService;

    public LoginCommandHandler(AuthDbContext context, IJwtService jwtService)
    {
        _context = context;
        _jwtService = jwtService;
    }

    public async Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username && !u.IsDeleted, cancellationToken);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Result<AuthResponse>.Failure("Invalid username or password");

        if (user.Status == UserStatus.Banned)
            return Result<AuthResponse>.Failure("Account is banned");

        var accessToken = _jwtService.GenerateAccessToken(user.Id, user.Username, user.Role.ToString());
        var refreshTokenValue = _jwtService.GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow.AddMinutes(60);

        var refreshToken = RefreshToken.Create(user.Id, refreshTokenValue, DateTime.UtcNow.AddDays(7));
        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync(cancellationToken);

        var userDto = new UserDto(user.Id, user.Username, user.FullName, user.Phone, user.Role.ToString(), user.Status.ToString());
        return Result<AuthResponse>.Success(new AuthResponse(accessToken, refreshTokenValue, expiresAt, userDto));
    }
}

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    private readonly AuthDbContext _context;
    private readonly IJwtService _jwtService;

    public RefreshTokenCommandHandler(AuthDbContext context, IJwtService jwtService)
    {
        _context = context;
        _jwtService = jwtService;
    }

    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var refreshToken = await _context.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == request.RefreshToken, cancellationToken);

        if (refreshToken == null || !refreshToken.IsActive)
            return Result<AuthResponse>.Failure("Invalid or expired refresh token");

        refreshToken.Revoke();

        var newAccessToken = _jwtService.GenerateAccessToken(refreshToken.User.Id, refreshToken.User.Username, refreshToken.User.Role.ToString());
        var newRefreshTokenValue = _jwtService.GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow.AddMinutes(60);

        var newRefreshToken = RefreshToken.Create(refreshToken.UserId, newRefreshTokenValue, DateTime.UtcNow.AddDays(7));
        _context.RefreshTokens.Add(newRefreshToken);
        await _context.SaveChangesAsync(cancellationToken);

        var userDto = new UserDto(refreshToken.User.Id, refreshToken.User.Username, refreshToken.User.FullName, refreshToken.User.Phone, refreshToken.User.Role.ToString(), refreshToken.User.Status.ToString());
        return Result<AuthResponse>.Success(new AuthResponse(newAccessToken, newRefreshTokenValue, expiresAt, userDto));
    }
}

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly AuthDbContext _context;

    public LogoutCommandHandler(AuthDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var token = await _context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == request.RefreshToken, cancellationToken);
        if (token == null) return Result.Failure("Token not found");

        token.Revoke();
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
