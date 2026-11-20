namespace FlowDesk.Shared.Contracts;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);

public sealed record UserDto(
    Guid Id,
    string Email,
    string FullName,
    bool IsActive,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

public sealed record CreateUserRequest(string Email, string FullName, string Password, IReadOnlyList<string> Roles);

public sealed record UpdateUserRequest(IReadOnlyList<string> Roles, bool IsActive);

public sealed record RoleDto(string Name, string Description, IReadOnlyList<string> Permissions);

public sealed record UserLookupDto(Guid Id, string FullName, IReadOnlyList<string> Roles);
