-- Seed: permisos y roles (equivalente a DbSeeder). Idempotente.
-- Los usuarios se crean desde la aplicación o la API (el hash de contraseña se calcula en .NET).
SET NOCOUNT ON;
GO

MERGE dbo.Permissions AS t
USING (VALUES
    (N'expedients.read', N'Consultar expedientes'),
    (N'expedients.create', N'Crear expedientes'),
    (N'expedients.assign', N'Asignar responsables'),
    (N'documents.upload', N'Cargar y digitalizar documentos'),
    (N'workflow.validate', N'Validar expedientes'),
    (N'workflow.approve', N'Aprobar o rechazar expedientes'),
    (N'audit.read', N'Consultar auditoría'),
    (N'users.manage', N'Administrar usuarios y roles')
) AS s (Code, Description)
ON t.Code = s.Code
WHEN NOT MATCHED THEN INSERT (Id, Code, Description) VALUES (NEWID(), s.Code, s.Description);
GO

MERGE dbo.Roles AS t
USING (VALUES
    (N'Administrador', N'Configuración y usuarios'),
    (N'Operador', N'Crea expedientes y digitaliza'),
    (N'Validador', N'Revisa documentos'),
    (N'Aprobador', N'Aprueba o rechaza'),
    (N'Auditor', N'Consulta historial')
) AS s (Name, Description)
ON t.Name = s.Name
WHEN NOT MATCHED THEN INSERT (Id, Name, Description) VALUES (NEWID(), s.Name, s.Description);
GO

INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM (VALUES
    (N'Administrador', N'expedients.read'),
    (N'Administrador', N'expedients.create'),
    (N'Administrador', N'expedients.assign'),
    (N'Administrador', N'documents.upload'),
    (N'Administrador', N'workflow.validate'),
    (N'Administrador', N'workflow.approve'),
    (N'Administrador', N'audit.read'),
    (N'Administrador', N'users.manage'),
    (N'Operador', N'expedients.read'),
    (N'Operador', N'expedients.create'),
    (N'Operador', N'documents.upload'),
    (N'Validador', N'expedients.read'),
    (N'Validador', N'expedients.assign'),
    (N'Validador', N'workflow.validate'),
    (N'Validador', N'documents.upload'),
    (N'Aprobador', N'expedients.read'),
    (N'Aprobador', N'workflow.approve'),
    (N'Auditor', N'expedients.read'),
    (N'Auditor', N'audit.read')
) AS m (RoleName, PermissionCode)
JOIN dbo.Roles r ON r.Name = m.RoleName
JOIN dbo.Permissions p ON p.Code = m.PermissionCode
WHERE NOT EXISTS (SELECT 1 FROM dbo.RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
GO
