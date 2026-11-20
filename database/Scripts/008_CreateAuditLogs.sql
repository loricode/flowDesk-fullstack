-- =============================================
-- 008_CreateAuditLogs.sql
-- Auditoría de solo inserción: un trigger impide modificar o borrar registros.
-- Idempotente: puede ejecutarse más de una vez.
-- =============================================
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLogs (
        Id            uniqueidentifier NOT NULL,
        OccurredAt    datetimeoffset   NOT NULL,   -- UTC
        UserId        uniqueidentifier NULL,
        UserName      nvarchar(150)    NULL,
        Action        nvarchar(60)     NOT NULL,
        EntityType    nvarchar(60)     NOT NULL,
        EntityId      uniqueidentifier NULL,
        PreviousState nvarchar(200)    NULL,
        NewState      nvarchar(200)    NULL,
        Details       nvarchar(1000)   NULL,
        CorrelationId nvarchar(64)     NULL,
        CONSTRAINT PK_AuditLogs PRIMARY KEY (Id)
    );

    CREATE INDEX IX_AuditLogs_OccurredAt ON dbo.AuditLogs (OccurredAt);
    CREATE INDEX IX_AuditLogs_Entity_OccurredAt ON dbo.AuditLogs (EntityId, OccurredAt);
END
GO

CREATE OR ALTER TRIGGER dbo.TR_AuditLogs_Immutable
ON dbo.AuditLogs
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 50001, N'La auditoría es inalterable: no se permiten UPDATE ni DELETE en AuditLogs.', 1;
END
GO

-- Recomendado en producción: el usuario de la aplicación solo puede insertar y leer.
-- DENY UPDATE, DELETE ON dbo.AuditLogs TO [flowdesk_app];
