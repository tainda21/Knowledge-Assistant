namespace WebAPI.DTOs;

public sealed record LoginRequest(
    string Username,
    string Password
);

public sealed record UserDto(
    Guid Id,
    string Username,
    string Role,
    DateTimeOffset CreatedAt
);

public sealed record LoginResponse(
    string Token,
    UserDto User
);

public sealed record CreateUserRequest(
    string Username,
    string Password,
    string Role
);
