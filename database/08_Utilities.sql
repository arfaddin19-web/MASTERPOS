/* ============================================================================
   MasterPOS — 08_Utilities.sql
   Printers, Payment Modes, Backup Log, Audit Log.
   Depends on: 01_Core_Auth.sql
   ============================================================================ */

SET QUOTED_IDENTIFIER ON;
GO

-- ---------------------------------------------------------------------------
-- Utility.Printers
-- ---------------------------------------------------------------------------
CREATE TABLE Utility.Printers
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Printers_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    BranchId          UNIQUEIDENTIFIER NOT NULL,
    Name              NVARCHAR(100)    NOT NULL,
    PrinterType       NVARCHAR(20)     NOT NULL CONSTRAINT CK_Printers_PrinterType
                          CHECK (PrinterType IN ('Receipt', 'Kot')),
    Station           NVARCHAR(20)     NULL CONSTRAINT CK_Printers_Station
                          CHECK (Station IS NULL OR Station IN ('Kitchen', 'Bar')),  -- only meaningful when PrinterType = 'Kot'
    ConnectionInfo    NVARCHAR(200)    NULL,             -- IP:port, USB path, etc.
    IsEnabled         BIT              NOT NULL CONSTRAINT DF_Printers_IsEnabled DEFAULT (1),
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_Printers_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc     DATETIME2(3)     NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL CONSTRAINT DF_Printers_IsDeleted DEFAULT (0),
    CONSTRAINT PK_Printers PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Printers_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT FK_Printers_Branch FOREIGN KEY (BranchId) REFERENCES Core.Branches(Id)
);
GO
GO

-- ---------------------------------------------------------------------------
-- Utility.PaymentModes
-- Seeded with Cash/Card/eSewa/Khalti/BankTransfer; a company can disable
-- ones it doesn't accept. The CHECK constraints on OrderPayments/
-- PartyPayments.PaymentMode list the same fixed set — keep both in sync if
-- a new mode is ever added.
-- ---------------------------------------------------------------------------
CREATE TABLE Utility.PaymentModes
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PaymentModes_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    Code              NVARCHAR(20)     NOT NULL,        -- Cash, Card, eSewa, Khalti, BankTransfer
    IsEnabled         BIT              NOT NULL CONSTRAINT DF_PaymentModes_IsEnabled DEFAULT (1),
    CONSTRAINT PK_PaymentModes PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_PaymentModes_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT UQ_PaymentModes_Company_Code UNIQUE (CompanyId, Code)
);
GO
GO

-- ---------------------------------------------------------------------------
-- Utility.BackupLog
-- ---------------------------------------------------------------------------
CREATE TABLE Utility.BackupLog
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_BackupLog_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    BackupAtUtc       DATETIME2(3)     NOT NULL CONSTRAINT DF_BackupLog_BackupAtUtc DEFAULT (SYSUTCDATETIME()),
    FilePath          NVARCHAR(400)    NOT NULL,
    SizeBytes         BIGINT           NULL,
    TriggeredByUserId UNIQUEIDENTIFIER NULL,             -- NULL = automatic/scheduled backup
    Status            NVARCHAR(20)     NOT NULL CONSTRAINT DF_BackupLog_Status DEFAULT ('Success')
                          CONSTRAINT CK_BackupLog_Status CHECK (Status IN ('Success', 'Failed')),
    CONSTRAINT PK_BackupLog PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_BackupLog_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id)
);
GO
GO

-- ---------------------------------------------------------------------------
-- Utility.AuditLog
-- Backs the Settings → Audit Trail screen. Application-level writes only
-- (not a DB-trigger audit) — keeps this readable ("Sneha Naik posted
-- Journal Entry #JE-0442") instead of raw column diffs.
-- ---------------------------------------------------------------------------
CREATE TABLE Utility.AuditLog
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_AuditLog_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    UserId            UNIQUEIDENTIFIER NOT NULL,
    Action            NVARCHAR(50)     NOT NULL,        -- Created, Updated, Deleted, Posted, Approved, ...
    EntityType        NVARCHAR(60)     NOT NULL,        -- "Accounting.JournalEntries", "Masters.Products", ...
    EntityId          UNIQUEIDENTIFIER NULL,
    Description        NVARCHAR(400)   NOT NULL,        -- "posted Journal Entry #JE-0442"
    OccurredAtUtc      DATETIME2(3)    NOT NULL CONSTRAINT DF_AuditLog_OccurredAtUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_AuditLog PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_AuditLog_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT FK_AuditLog_User FOREIGN KEY (UserId) REFERENCES Auth.Users(Id)
);
GO
CREATE INDEX IX_AuditLog_Company_OccurredAt ON Utility.AuditLog(CompanyId, OccurredAtUtc DESC);
GO
