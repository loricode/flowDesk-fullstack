-- =============================================
-- 007_CreateApprovals.sql
-- Decisiones de aprobación y notificaciones a usuarios.
-- Idempotente: puede ejecutarse más de una vez.
-- =============================================
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.Approvals', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Approvals (
        Id          uniqueidentifier NOT NULL,
        ExpedientId uniqueidentifier NOT NULL,
        Decision    nvarchar(30)     NOT NULL,
        DecidedById uniqueidentifier NOT NULL,
        Comment     nvarchar(1000)   NULL,
        DecidedAt   datetimeoffset   NOT NULL,
        CONSTRAINT PK_Approvals PRIMARY KEY (Id),
        CONSTRAINT FK_Approvals_Expedients_ExpedientId FOREIGN KEY (ExpedientId) REFERENCES dbo.Expedients (Id) ON DELETE CASCADE,
        CONSTRAINT FK_Approvals_Users_DecidedById FOREIGN KEY (DecidedById) REFERENCES dbo.Users (Id),
        CONSTRAINT CK_Approvals_Decision CHECK (Decision IN (N'Approved', N'Rejected', N'CorrectionRequested'))
    );

    CREATE INDEX IX_Approvals_ExpedientId ON dbo.Approvals (ExpedientId);
    CREATE INDEX IX_Approvals_DecidedById ON dbo.Approvals (DecidedById);
END
GO

IF OBJECT_ID(N'dbo.Notifications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notifications (
        Id          uniqueidentifier NOT NULL,
        UserId      uniqueidentifier NOT NULL,
        Title       nvarchar(150)    NOT NULL,
        Message     nvarchar(500)    NOT NULL,
        ExpedientId uniqueidentifier NULL,
        IsRead      bit              NOT NULL,
        CreatedAt   datetimeoffset   NOT NULL,
        ReadAt      datetimeoffset   NULL,
        CONSTRAINT PK_Notifications PRIMARY KEY (Id),
        CONSTRAINT FK_Notifications_Users_UserId FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_Notifications_User_IsRead_CreatedAt ON dbo.Notifications (UserId, IsRead, CreatedAt);
END
GO
