namespace Greenhouse.Core.Contracts;

public sealed record LoginRequest(string? Username, string? Password);

public sealed record RefreshRequest(string? RefreshToken);

public sealed record AuthResponse(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, DateTime RefreshTokenExpiresAt, string Username, string Role);

public sealed record UserIn(string? Username, string? Password, string? Role);

public sealed record UserOut(int Id, string Username, string Role, DateTime CreatedAt);
