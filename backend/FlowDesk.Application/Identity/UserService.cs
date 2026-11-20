using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Domain.Audit;
using FlowDesk.Domain.Identity;
using FlowDesk.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Identity;

public sealed class UserService(IAppDbContext db, IPasswordHasher hasher, IAuditWriter audit, IClock clock)
{
    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking()
            .Include(u => u.Roles).ThenInclude(r => r.Permissions)
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);
        return users.Select(u => u.ToDto()).ToList();
    }

    /// <summary>Usuarios activos a los que se puede asignar un expediente.</summary>
    public async Task<IReadOnlyList<UserLookupDto>> LookupAsync(string? permission, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking().Where(u => u.IsActive);
        if (!string.IsNullOrWhiteSpace(permission))
            query = query.Where(u => u.Roles.Any(r => r.Permissions.Any(p => p.Code == permission)));

        return await query
            .OrderBy(u => u.FullName)
            .Select(u => new UserLookupDto(u.Id, u.FullName, u.Roles.Select(r => r.Name).ToList()))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken ct) =>
        await db.Roles.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Name, r.Description, r.Permissions.Select(p => p.Code).ToList()))
            .ToListAsync(ct);

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@')) errors["email"] = ["Correo inválido."];
        if (string.IsNullOrWhiteSpace(request.FullName)) errors["fullName"] = ["El nombre es obligatorio."];
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 8) errors["password"] = ["Mínimo 8 caracteres."];
        if (errors.Count > 0) throw new ValidationException(errors);

        var normalized = User.Normalize(request.Email);
        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized, ct))
            throw new ValidationException("email", "Ya existe un usuario con ese correo.");

        var user = new User(request.Email, request.FullName, hasher.Hash(request.Password), clock.UtcNow);
        user.SetRoles(await LoadRolesAsync(request.Roles, ct));
        db.Users.Add(user);

        audit.Write(AuditActions.UserCreated, nameof(User), user.Id, newState: string.Join(",", request.Roles));
        await db.SaveChangesAsync(ct);
        return user.ToDto();
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        var user = await db.Users
            .Include(u => u.Roles).ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException("Usuario", id);

        var previous = $"{string.Join(",", user.Roles.Select(r => r.Name))}|active={user.IsActive}";
        user.SetRoles(await LoadRolesAsync(request.Roles, ct));
        user.SetActive(request.IsActive);

        audit.Write(AuditActions.UserUpdated, nameof(User), user.Id, previous,
            $"{string.Join(",", request.Roles)}|active={request.IsActive}");
        await db.SaveChangesAsync(ct);
        return user.ToDto();
    }

    private async Task<List<Role>> LoadRolesAsync(IReadOnlyList<string> names, CancellationToken ct)
    {
        var roles = await db.Roles.Include(r => r.Permissions).Where(r => names.Contains(r.Name)).ToListAsync(ct);
        var missing = names.Except(roles.Select(r => r.Name)).ToList();
        if (missing.Count > 0) throw new ValidationException("roles", $"Roles inexistentes: {string.Join(", ", missing)}");
        return roles;
    }
}
