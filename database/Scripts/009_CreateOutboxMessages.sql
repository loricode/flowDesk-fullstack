-- =============================================
-- 009_CreateOutboxMessages.sql
-- Patrón Outbox (eventos pendientes de publicar) e idempotencia del consumidor.
-- Idempotente: puede ejecutarse más de una vez.
-- =============================================
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.OutboxMessages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OutboxMessages (
        Id            uniqueidentifier NOT NULL,   -- = EventId del evento (deduplicación en SQS FIFO)
        Type          nvarchar(200)    NOT NULL,
        Payload       nvarchar(max)    NOT NULL,   -- JSON
        OccurredAt    datetimeoffset   NOT NULL,
        ProcessedAt   datetimeoffset   NULL,
        Attempts      int              NOT NULL CONSTRAINT DF_OutboxMessages_Attempts DEFAULT 0,
        LastError     nvarchar(2000)   NULL,
        CorrelationId nvarchar(64)     NULL,
        CONSTRAINT PK_OutboxMessages PRIMARY KEY (Id),
        CONSTRAINT CK_OutboxMessages_Payload CHECK (ISJSON(Payload) = 1)
    );

    -- Índice del modelo EF + índice filtrado para la consulta del dispatcher.
    CREATE INDEX IX_OutboxMessages_Pending ON dbo.OutboxMessages (ProcessedAt, OccurredAt);
    CREATE INDEX IX_OutboxMessages_Unprocessed ON dbo.OutboxMessages (OccurredAt) INCLUDE (Attempts) WHERE ProcessedAt IS NULL;
END
GO

IF OBJECT_ID(N'dbo.ProcessedMessages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProcessedMessages (
        MessageId   uniqueidentifier NOT NULL,
        Consumer    nvarchar(100)    NOT NULL,
        ProcessedAt datetimeoffset   NOT NULL,
        CONSTRAINT PK_ProcessedMessages PRIMARY KEY (MessageId, Consumer)
    );
END
GO
