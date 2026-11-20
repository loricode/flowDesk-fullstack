using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Identity;
using FlowDesk.Domain.Workflow;
using FlowDesk.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using P = FlowDesk.Shared.Contracts.FlowDeskPermissions;

namespace FlowDesk.Infrastructure.Persistence;

/// <summary>
/// Carga datos iniciales de forma idempotente: permisos, roles, workflow y usuarios de demostración.
/// Equivale a database/Seed/*.sql, salvo los usuarios, cuyo hash de contraseña se calcula aquí.
/// </summary>
public sealed class DbSeeder(FlowDeskDbContext db, IPasswordHasher hasher, IClock clock, ILogger<DbSeeder> logger)
{
    private static readonly Dictionary<string, string> PermissionDescriptions = new()
    {
        [P.ExpedientsRead] = "Consultar expedientes",
        [P.ExpedientsCreate] = "Crear expedientes",
        [P.ExpedientsAssign] = "Asignar responsables",
        [P.DocumentsUpload] = "Cargar y digitalizar documentos",
        [P.WorkflowValidate] = "Validar expedientes",
        [P.WorkflowApprove] = "Aprobar o rechazar expedientes",
        [P.AuditRead] = "Consultar auditoría",
        [P.UsersManage] = "Administrar usuarios y roles",
    };

    private static readonly (string Role, string Description, string[] Permissions)[] RoleMatrix =
    [
        (FlowDeskRoles.Administrator, "Configuración y usuarios", [.. P.All]),
        (FlowDeskRoles.Operator, "Crea expedientes y digitaliza", [P.ExpedientsRead, P.ExpedientsCreate, P.DocumentsUpload]),
        (FlowDeskRoles.Validator, "Revisa documentos", [P.ExpedientsRead, P.ExpedientsAssign, P.WorkflowValidate, P.DocumentsUpload]),
        (FlowDeskRoles.Approver, "Aprueba o rechaza", [P.ExpedientsRead, P.WorkflowApprove]),
        (FlowDeskRoles.Auditor, "Consulta historial", [P.ExpedientsRead, P.AuditRead]),
    ];

    private static readonly (string Email, string Name, string Password, string Role)[] DemoUsers =
    [
        ("admin@flowdesk.local", "Ana Administradora", "Admin123!", FlowDeskRoles.Administrator),
        ("operador@flowdesk.local", "Óscar Operador", "Operador123!", FlowDeskRoles.Operator),
        ("validador@flowdesk.local", "Valeria Validadora", "Validador123!", FlowDeskRoles.Validator),
        ("aprobador@flowdesk.local", "Andrés Aprobador", "Aprobador123!", FlowDeskRoles.Approver),
        ("auditor@flowdesk.local", "Laura Auditora", "Auditor123!", FlowDeskRoles.Auditor),
    ];

    public async Task SeedAsync(bool includeDemoUsers, CancellationToken ct = default)
    {
        var permissions = await db.Permissions.ToListAsync(ct);
        foreach (var (code, description) in PermissionDescriptions.Where(p => permissions.All(x => x.Code != p.Key)))
        {
            var permission = new Permission(code, description);
            db.Permissions.Add(permission);
            permissions.Add(permission);
        }

        var roles = await db.Roles.Include(r => r.Permissions).ToListAsync(ct);
        foreach (var (name, description, codes) in RoleMatrix)
        {
            var role = roles.FirstOrDefault(r => r.Name == name);
            if (role is null)
            {
                role = new Role(name, description);
                db.Roles.Add(role);
                roles.Add(role);
            }
            foreach (var code in codes.Where(c => role.Permissions.All(p => p.Code != c)))
                role.Permissions.Add(permissions.Single(p => p.Code == code));
        }

        if (!await db.WorkflowDefinitions.AnyAsync(w => w.Code == WorkflowDefinition.DefaultCode, ct))
            db.WorkflowDefinitions.Add(WorkflowDefinition.CreateDefault(P.WorkflowValidate, P.WorkflowApprove, P.DocumentsUpload));

        if (includeDemoUsers)
        {
            foreach (var (email, name, password, roleName) in DemoUsers)
            {
                var normalized = User.Normalize(email);
                if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized, ct)) continue;

                var user = new User(email, name, hasher.Hash(password), clock.UtcNow);
                user.SetRoles([roles.Single(r => r.Name == roleName)]);
                db.Users.Add(user);
            }
        }

        var changes = await db.SaveChangesAsync(ct);
        if (changes > 0) logger.LogInformation("Seed aplicado: {Changes} cambios.", changes);
    }
}
