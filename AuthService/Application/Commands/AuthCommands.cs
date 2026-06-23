using MediatR;
using SharedKernel.Results;
using AuthService.Application.DTOs;

namespace AuthService.Application.Commands;

public record RegisterCommand(
    string Username,
    string Password,
    string FullName,
    string Phone,
    string Role
) : IRequest<Result<UserDto>>;

public record LoginCommand(
    string Username,
    string Password
) : IRequest<Result<AuthResponse>>;

public record RefreshTokenCommand(
    string RefreshToken
) : IRequest<Result<AuthResponse>>;

public record LogoutCommand(
    string RefreshToken
) : IRequest<Result>;
