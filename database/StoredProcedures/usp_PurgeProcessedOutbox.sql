-- Mantenimiento: elimina mensajes del Outbox ya publicados con más de @RetentionDays días.
-- Programar con SQL Agent o EventBridge Scheduler. Borra por lotes para no bloquear la tabla.
CREATE OR ALTER PROCEDURE dbo.usp_PurgeProcessedOutbox
    @RetentionDays int = 7,
    @BatchSize     int = 5000
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @cutoff datetimeoffset = DATEADD(DAY, -@RetentionDays, SYSDATETIMEOFFSET());
    DECLARE @deleted int = 1, @total int = 0;

    WHILE @deleted > 0
    BEGIN
        DELETE TOP (@BatchSize) FROM dbo.OutboxMessages
        WHERE ProcessedAt IS NOT NULL AND ProcessedAt < @cutoff;

        SET @deleted = @@ROWCOUNT;
        SET @total += @deleted;
    END

    DELETE FROM dbo.ProcessedMessages WHERE ProcessedAt < @cutoff;
    SELECT @total AS DeletedOutboxMessages;
END
GO
