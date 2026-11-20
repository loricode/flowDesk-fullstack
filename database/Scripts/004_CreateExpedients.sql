-- =============================================
-- 004_CreateExpedients.sql
-- Expedientes e historial de estados.
-- Idempotente: puede ejecutarse más de una vez.
-- =============================================
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.Expedients', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Expedients (
        Id               uniqueidentifier NOT NULL,
        Code             nvarchar(30)     NOT NULL,
        Title            nvarchar(200)    NOT NULL,
        ApplicantName    nvarchar(150)    NOT NULL,
        Description      nvarchar(2000)   NULL,
        Priority         nvarchar(20)     NOT NULL,
        Status           nvarchar(30)     NOT NULL,
        CreatedById      uniqueidentifier NOT NULL,
        AssignedToId     uniqueidentifier NULL,
        CreatedAt        datetimeoffset   NOT NULL,
        UpdatedAt        datetimeoffset   NOT NULL,
        ClosedAt         datetimeoffset   NULL,
        ConcurrencyStamp uniqueidentifier NOT NULL,  -- Concurrencia optimista (se rota en cada cambio)
        CONSTRAINT PK_Expedients PRIMARY KEY (Id),
        CONSTRAINT FK_Expedients_Users_CreatedById FOREIGN KEY (CreatedById) REFERENCES dbo.Users (Id),
        CONSTRAINT FK_Expedients_Users_AssignedToId FOREIGN KEY (AssignedToId) REFERENCES dbo.Users (Id),
        CONSTRAINT CK_Expedients_Status CHECK (Status IN (N'Received', N'InValidation', N'PendingApproval', N'CorrectionRequested', N'Approved', N'Rejected')),
        CONSTRAINT CK_Expedients_Priority CHECK (Priority IN (N'Low', N'Normal', N'High', N'Urgent')),
        CONSTRAINT CK_Expedients_ClosedAt CHECK (
            (Status IN (N'Approved', N'Rejected') AND ClosedAt IS NOT NULL) OR
            (Status NOT IN (N'Approved', N'Rejected') AND ClosedAt IS NULL))
    );

    CREATE UNIQUE INDEX UX_Expedients_Code ON dbo.Expedients (Code);
    CREATE INDEX IX_Expedients_Status_Priority_UpdatedAt ON dbo.Expedients (Status, Priority, UpdatedAt);
    CREATE INDEX IX_Expedients_AssignedTo_Status ON dbo.Expedients (AssignedToId, Status);
    CREATE INDEX IX_Expedients_CreatedById ON dbo.Expedients (CreatedById);
END
GO

IF OBJECT_ID(N'dbo.ExpedientHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExpedientHistory (
        Id          uniqueidentifier NOT NULL,
        ExpedientId uniqueidentifier NOT NULL,
        FromStatus  nvarchar(30)     NULL,
        ToStatus    nvarchar(30)     NOT NULL,
        ChangedById uniqueidentifier NOT NULL,
        Comment     nvarchar(1000)   NULL,
        OccurredAt  datetimeoffset   NOT NULL,
        CONSTRAINT PK_ExpedientHistory PRIMARY KEY (Id),
        CONSTRAINT FK_ExpedientHistory_Expedients_ExpedientId FOREIGN KEY (ExpedientId) REFERENCES dbo.Expedients (Id) ON DELETE CASCADE,
        CONSTRAINT FK_ExpedientHistory_Users_ChangedById FOREIGN KEY (ChangedById) REFERENCES dbo.Users (Id),
        CONSTRAINT CK_ExpedientHistory_ToStatus CHECK (ToStatus IN (N'Received', N'InValidation', N'PendingApproval', N'CorrectionRequested', N'Approved', N'Rejected'))
    );

    CREATE INDEX IX_ExpedientHistory_Expedient_OccurredAt ON dbo.ExpedientHistory (ExpedientId, OccurredAt);
    CREATE INDEX IX_ExpedientHistory_ChangedById ON dbo.ExpedientHistory (ChangedById);
END
GO
