-- =============================================
-- 000_CreateSchemaVersions.sql
-- Registro de scripts aplicados (usado por deploy.ps1).
-- Idempotente: puede ejecutarse más de una vez.
-- =============================================
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.SchemaVersions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaVersions (
        ScriptName  nvarchar(200)  NOT NULL CONSTRAINT PK_SchemaVersions PRIMARY KEY,
        AppliedAt   datetimeoffset NOT NULL CONSTRAINT DF_SchemaVersions_AppliedAt DEFAULT SYSDATETIMEOFFSET(),
        AppliedBy   nvarchar(128)  NOT NULL CONSTRAINT DF_SchemaVersions_AppliedBy DEFAULT SUSER_SNAME(),
        Checksum    char(64)       NULL
    );
END
GO
