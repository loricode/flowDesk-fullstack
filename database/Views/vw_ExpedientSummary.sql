-- Vista de reporting: resumen de expedientes con responsable y conteo de documentos.
CREATE OR ALTER VIEW dbo.vw_ExpedientSummary
AS
SELECT
    e.Id,
    e.Code,
    e.Title,
    e.ApplicantName,
    e.Status,
    e.Priority,
    assignee.FullName AS AssignedToName,
    creator.FullName  AS CreatedByName,
    (SELECT COUNT(*) FROM dbo.Documents d WHERE d.ExpedientId = e.Id)                           AS DocumentCount,
    (SELECT COUNT(*) FROM dbo.Documents d WHERE d.ExpedientId = e.Id AND d.Status = N'Invalid') AS InvalidDocumentCount,
    e.CreatedAt,
    e.UpdatedAt,
    e.ClosedAt,
    DATEDIFF(HOUR, e.CreatedAt, COALESCE(e.ClosedAt, SYSDATETIMEOFFSET())) AS AgeHours
FROM dbo.Expedients e
JOIN dbo.Users creator       ON creator.Id = e.CreatedById
LEFT JOIN dbo.Users assignee ON assignee.Id = e.AssignedToId;
GO
