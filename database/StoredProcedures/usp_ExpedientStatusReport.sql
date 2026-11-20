-- Reporte de expedientes por estado y tiempo medio de resolución en un rango de fechas.
CREATE OR ALTER PROCEDURE dbo.usp_ExpedientStatusReport
    @From datetimeoffset,
    @To   datetimeoffset
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        Status,
        COUNT(*) AS Total,
        AVG(CASE WHEN ClosedAt IS NOT NULL THEN DATEDIFF(MINUTE, CreatedAt, ClosedAt) / 60.0 END) AS AvgResolutionHours
    FROM dbo.Expedients
    WHERE CreatedAt >= @From AND CreatedAt < @To
    GROUP BY Status
    ORDER BY Status;
END
GO
