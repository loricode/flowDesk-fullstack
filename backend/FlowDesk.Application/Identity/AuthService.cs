using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Domain.Audit;
using FlowDesk.Domain.Identity;
using FlowDesk.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Identity;

public sealed class AuthService(
    IAppDbContext db,
    IPasswordHasher hasher,
    ITokenService tokens,
    IAuditWriter audit,
    ICurrentUser currentUser,
    IClock clock)
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            throw new ValidationException("credentials", "Correo y contraseña son obligatorios.");

        var normalized = User.Normalize(request.Email);
        var user = await db.Users
            .Include(u => u.Roles).ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);

        if (user is null || !user.IsActive || !hasher.Verify(request.Password, user.PasswordHash))
        {
            audit.Write(AuditActions.LoginFailed, nameof(User), user?.Id,
                details: "Credenciales inválidas", actorId: user?.Id, actorName: request.Email.Trim());
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException("Credenciales inválidas.");
        }

        user.RegisterLogin(clock.UtcNow);
        audit.Write(AuditActions.Login, nameof(User), user.Id, actorId: user.Id, actorName: user.FullName);
        await db.SaveChangesAsync(ct);

        var (token, expiresAt) = tokens.CreateToken(user);
        return new LoginResponse(token, expiresAt, user.ToDto());
    }

    public async Task<UserDto> GetCurrentAsync(CancellationToken ct)
    {
        var id = currentUser.UserId ?? throw new UnauthorizedException("No autenticado.");
        var user = await db.Users.AsNoTracking()
            .Include(u => u.Roles).ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException("Usuario", id);
        return user.ToDto();
    }
}

internal static class UserMappings
{
    public static UserDto ToDto(this User user) => new(
        user.Id, user.Email, user.FullName, user.IsActive,
        user.Roles.Select(r => r.Name).Order().ToList(),
        user.PermissionCodes);
}
