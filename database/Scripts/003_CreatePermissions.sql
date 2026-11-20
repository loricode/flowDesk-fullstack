-- =============================================
-- 003_CreatePermissions.sql
-- Permisos y relación rol-permiso.
-- Idempotente: puede ejecutarse más de una vez.
-- =============================================
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.Permissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Permissions (
        Id          uniqueidentifier NOT NULL,
        Code        nvarchar(100)    NOT NULL,   -- p. ej. 'workflow.approve'
        Description nvarchar(250)    NOT NULL,
        CONSTRAINT PK_Permissions PRIMARY KEY (Id)
    );

    CREATE UNIQUE INDEX UX_Permissions_Code ON dbo.Permissions (Code);
END
GO

IF OBJECT_ID(N'dbo.RolePermissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RolePermissions (
        RoleId       uniqueidentifier NOT NULL,
        PermissionId uniqueidentifier NOT NULL,
        CONSTRAINT PK_RolePermissions PRIMARY KEY (RoleId, PermissionId),
        CONSTRAINT FK_RolePermissions_Roles_RoleId FOREIGN KEY (RoleId) REFERENCES dbo.Roles (Id) ON DELETE CASCADE,
        CONSTRAINT FK_RolePermissions_Permissions_PermissionId FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions (Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_RolePermissions_PermissionId ON dbo.RolePermissions (PermissionId);
END
GO
