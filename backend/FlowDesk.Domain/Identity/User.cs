using FlowDesk.Domain.Common;

namespace FlowDesk.Domain.Identity;

public sealed class User : AggregateRoot
{
    private User() { }

    public User(string email, string fullName, string passwordHash, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new DomainException("El correo es obligatorio.");
        if (string.IsNullOrWhiteSpace(fullName)) throw new DomainException("El nombre es obligatorio.");

        Email = email.Trim();
        NormalizedEmail = Normalize(email);
        FullName = fullName.Trim();
        PasswordHash = passwordHash;
        IsActive = true;
        CreatedAt = now;
    }

    public string Email { get; private set; } = default!;
    public string NormalizedEmail { get; private set; } = default!;
    public string FullName { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }

    public List<Role> Roles { get; private set; } = [];

    public static string Normalize(string email) => email.Trim().ToUpperInvariant();

    public IReadOnlyList<string> PermissionCodes =>
        Roles.SelectMany(r => r.Permissions).Select(p => p.Code).Distinct().Order().ToList();

    public void RegisterLogin(DateTimeOffset now) => LastLoginAt = now;

    public void SetRoles(IEnumerable<Role> roles)
    {
        Roles.Clear();
        Roles.AddRange(roles);
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}

public sealed class Role : Entity
{
    private Role() { }

    public Role(string name, string description)
    {
        Name = name;
        Description = description;
    }

    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public List<Permission> Permissions { get; private set; } = [];
    public List<User> Users { get; private set; } = [];
}

public sealed class Permission : Entity
{
    private Permission() { }

    public Permission(string code, string description)
    {
        Code = code;
        Description = description;
    }

    public string Code { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public List<Role> Roles { get; private set; } = [];
}
