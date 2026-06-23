namespace AuthService.Application.DTOs;

public record RegisterRequest(
    string Username,
    string Password,
    string FullName,
    string Phone,
    string Role = "Customer"
);

public record LoginRequest(
    string Username,
    string Password
);

public record RefreshTokenRequest(
    string RefreshToken
);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User
);

public record UserDto(
    Guid Id,
    string Username,
    string FullName,
    string Phone,
    string Role,
    string Status
);
