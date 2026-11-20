-- =============================================
-- 005_CreateDocuments.sql
-- Documentos y versiones. El archivo físico vive en S3; aquí solo la referencia y la metadata.
-- Idempotente: puede ejecutarse más de una vez.
-- =============================================
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.Documents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Documents (
        Id                uniqueidentifier NOT NULL,
        ExpedientId       uniqueidentifier NOT NULL,
        FileName          nvarchar(255)    NOT NULL,
        ContentType       nvarchar(100)    NOT NULL,
        Status            nvarchar(20)     NOT NULL,
        ValidationMessage nvarchar(500)    NULL,
        CurrentVersion    int              NOT NULL,
        CreatedAt         datetimeoffset   NOT NULL,
        CONSTRAINT PK_Documents PRIMARY KEY (Id),
        CONSTRAINT FK_Documents_Expedients_ExpedientId FOREIGN KEY (ExpedientId) REFERENCES dbo.Expedients (Id) ON DELETE CASCADE,
        CONSTRAINT CK_Documents_Status CHECK (Status IN (N'Pending', N'Validated', N'Invalid')),
        CONSTRAINT CK_Documents_CurrentVersion CHECK (CurrentVersion >= 1)
    );

    -- Un mismo nombre dentro del expediente genera una nueva versión, no un documento duplicado.
    CREATE UNIQUE INDEX UX_Documents_Expedient_FileName ON dbo.Documents (ExpedientId, FileName);
END
GO

IF OBJECT_ID(N'dbo.DocumentVersions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentVersions (
        Id            uniqueidentifier NOT NULL,
        DocumentId    uniqueidentifier NOT NULL,
        VersionNumber int              NOT NULL,
        StorageKey    nvarchar(500)    NOT NULL,   -- Clave del objeto en S3
        SizeBytes     bigint           NOT NULL,
        ContentType   nvarchar(100)    NOT NULL,
        Sha256        nchar(64)        NULL,       -- Calculado por el Worker; detecta duplicados
        Source        nvarchar(20)     NOT NULL,
        UploadedById  uniqueidentifier NOT NULL,
        UploadedAt    datetimeoffset   NOT NULL,
        CONSTRAINT PK_DocumentVersions PRIMARY KEY (Id),
        CONSTRAINT FK_DocumentVersions_Documents_DocumentId FOREIGN KEY (DocumentId) REFERENCES dbo.Documents (Id) ON DELETE CASCADE,
        CONSTRAINT FK_DocumentVersions_Users_UploadedById FOREIGN KEY (UploadedById) REFERENCES dbo.Users (Id),
        CONSTRAINT CK_DocumentVersions_Source CHECK (Source IN (N'Web', N'DesktopFile', N'Scanner')),
        CONSTRAINT CK_DocumentVersions_Size CHECK (SizeBytes > 0 AND SizeBytes <= 20971520)
    );

    CREATE UNIQUE INDEX UX_DocumentVersions_Document_Version ON dbo.DocumentVersions (DocumentId, VersionNumber);
    CREATE INDEX IX_DocumentVersions_Sha256 ON dbo.DocumentVersions (Sha256) WHERE Sha256 IS NOT NULL;
    CREATE INDEX IX_DocumentVersions_UploadedById ON dbo.DocumentVersions (UploadedById);
END
GO
