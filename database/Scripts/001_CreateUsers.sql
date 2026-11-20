-- =============================================
-- 001_CreateUsers.sql
-- Usuarios del sistema (módulo Identity).
-- Idempotente: puede ejecutarse más de una vez.
-- =============================================
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users (
        Id              uniqueidentifier NOT NULL,
        Email           nvarchar(256)    NOT NULL,
        NormalizedEmail nvarchar(256)    NOT NULL,
        FullName        nvarchar(150)    NOT NULL,
        PasswordHash    nvarchar(500)    NOT NULL,   -- PBKDF2-SHA256: PBKDF2$iteraciones$sal$hash
        IsActive        bit              NOT NULL,
        CreatedAt       datetimeoffset   NOT NULL,
        LastLoginAt     datetimeoffset   NULL,
        CONSTRAINT PK_Users PRIMARY KEY (Id)
    );

    CREATE UNIQUE INDEX UX_Users_NormalizedEmail ON dbo.Users (NormalizedEmail);
END
GO
