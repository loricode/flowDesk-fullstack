-- =============================================
-- 002_CreateRoles.sql
-- Roles y relación usuario-rol.
-- Idempotente: puede ejecutarse más de una vez.
-- =============================================
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Roles (
        Id          uniqueidentifier NOT NULL,
        Name        nvarchar(50)     NOT NULL,
        Description nvarchar(250)    NOT NULL,
        CONSTRAINT PK_Roles PRIMARY KEY (Id)
    );

    CREATE UNIQUE INDEX UX_Roles_Name ON dbo.Roles (Name);
END
GO

IF OBJECT_ID(N'dbo.UserRoles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserRoles (
        UserId uniqueidentifier NOT NULL,
        RoleId uniqueidentifier NOT NULL,
        CONSTRAINT PK_UserRoles PRIMARY KEY (UserId, RoleId),
        CONSTRAINT FK_UserRoles_Users_UserId FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE,
        CONSTRAINT FK_UserRoles_Roles_RoleId FOREIGN KEY (RoleId) REFERENCES dbo.Roles (Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_UserRoles_RoleId ON dbo.UserRoles (RoleId);
END
GO
