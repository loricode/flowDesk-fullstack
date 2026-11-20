-- =============================================
-- 006_CreateWorkflows.sql
-- Definiciones de flujo y transiciones permitidas.
-- Idempotente: puede ejecutarse más de una vez.
-- =============================================
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.WorkflowDefinitions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkflowDefinitions (
        Id       uniqueidentifier NOT NULL,
        Code     nvarchar(50)     NOT NULL,
        Name     nvarchar(150)    NOT NULL,
        IsActive bit              NOT NULL,
        CONSTRAINT PK_WorkflowDefinitions PRIMARY KEY (Id)
    );

    CREATE UNIQUE INDEX UX_WorkflowDefinitions_Code ON dbo.WorkflowDefinitions (Code);
END
GO

IF OBJECT_ID(N'dbo.WorkflowTransitions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkflowTransitions (
        Id                   uniqueidentifier NOT NULL,
        WorkflowDefinitionId uniqueidentifier NOT NULL,
        Code                 nvarchar(50)     NOT NULL,
        Name                 nvarchar(100)    NOT NULL,
        FromStatus           nvarchar(30)     NOT NULL,
        ToStatus             nvarchar(30)     NOT NULL,
        RequiredPermission   nvarchar(100)    NOT NULL,
        RequiresComment      bit              NOT NULL,
        SortOrder            int              NOT NULL,
        CONSTRAINT PK_WorkflowTransitions PRIMARY KEY (Id),
        CONSTRAINT FK_WorkflowTransitions_WorkflowDefinitions_WorkflowDefinitionId
            FOREIGN KEY (WorkflowDefinitionId) REFERENCES dbo.WorkflowDefinitions (Id) ON DELETE CASCADE,
        CONSTRAINT CK_WorkflowTransitions_States CHECK (FromStatus <> ToStatus
            AND FromStatus IN (N'Received', N'InValidation', N'PendingApproval', N'CorrectionRequested', N'Approved', N'Rejected') AND ToStatus IN (N'Received', N'InValidation', N'PendingApproval', N'CorrectionRequested', N'Approved', N'Rejected'))
    );

    CREATE UNIQUE INDEX UX_WorkflowTransitions_Definition_Code ON dbo.WorkflowTransitions (WorkflowDefinitionId, Code);
END
GO
