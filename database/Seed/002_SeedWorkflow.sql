-- Seed: flujo estándar de expedientes (equivalente a WorkflowDefinition.CreateDefault). Idempotente.
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.WorkflowDefinitions WHERE Code = N'EXPEDIENT_DEFAULT')
    INSERT INTO dbo.WorkflowDefinitions (Id, Code, Name, IsActive)
    VALUES (NEWID(), N'EXPEDIENT_DEFAULT', N'Flujo estándar de expedientes', 1);
GO

DECLARE @wf uniqueidentifier = (SELECT Id FROM dbo.WorkflowDefinitions WHERE Code = N'EXPEDIENT_DEFAULT');

MERGE dbo.WorkflowTransitions AS t
USING (VALUES
    (N'start-validation', N'Iniciar validación', N'Received', N'InValidation', N'workflow.validate', 0, 1),
    (N'send-to-approval', N'Enviar a aprobación', N'InValidation', N'PendingApproval', N'workflow.validate', 0, 2),
    (N'request-correction', N'Solicitar corrección', N'InValidation', N'CorrectionRequested', N'workflow.validate', 1, 3),
    (N'reject-validation', N'Rechazar en validación', N'InValidation', N'Rejected', N'workflow.validate', 1, 4),
    (N'approve', N'Aprobar', N'PendingApproval', N'Approved', N'workflow.approve', 0, 5),
    (N'reject', N'Rechazar', N'PendingApproval', N'Rejected', N'workflow.approve', 1, 6),
    (N'return-correction', N'Devolver para corrección', N'PendingApproval', N'CorrectionRequested', N'workflow.approve', 1, 7),
    (N'resubmit', N'Reenviar a validación', N'CorrectionRequested', N'InValidation', N'documents.upload', 0, 8)
) AS s (Code, Name, FromStatus, ToStatus, RequiredPermission, RequiresComment, SortOrder)
ON t.WorkflowDefinitionId = @wf AND t.Code = s.Code
WHEN NOT MATCHED THEN
    INSERT (Id, WorkflowDefinitionId, Code, Name, FromStatus, ToStatus, RequiredPermission, RequiresComment, SortOrder)
    VALUES (NEWID(), @wf, s.Code, s.Name, s.FromStatus, s.ToStatus, s.RequiredPermission, s.RequiresComment, s.SortOrder);
GO
