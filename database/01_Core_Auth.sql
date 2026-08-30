/* ============================================================================
   MasterPOS — 01_Core_Auth.sql
   Schemas, Companies (Setup wizard output), Branches, Roles, RolePermissions, Users.
   Run this file first — everything else references Core.Companies.
   ============================================================================ */

SET QUOTED_IDENTIFIER ON;
GO

-- ---------------------------------------------------------------------------
-- Schemas (one CREATE SCHEMA per batch — SQL Server requirement)
-- ---------------------------------------------------------------------------
IF SCHEMA_ID('Core') IS NULL EXEC('CREATE SCHEMA Core');
GO
IF SCHEMA_ID('Auth') IS NULL EXEC('CREATE SCHEMA Auth');
GO
IF SCHEMA_ID('Masters') IS NULL EXEC('CREATE SCHEMA Masters');
GO
IF SCHEMA_ID('Sales') IS NULL EXEC('CREATE SCHEMA Sales');
GO
IF SCHEMA_ID('Purchase') IS NULL EXEC('CREATE SCHEMA Purchase');
GO
IF SCHEMA_ID('Accounting') IS NULL EXEC('CREATE SCHEMA Accounting');
GO
IF SCHEMA_ID('Inventory') IS NULL EXEC('CREATE SCHEMA Inventory');
GO
IF SCHEMA_ID('Workforce') IS NULL EXEC('CREATE SCHEMA Workforce');
GO
IF SCHEMA_ID('Utility') IS NULL EXEC('CREATE SCHEMA Utility');
GO

-- ---------------------------------------------------------------------------
-- Core.Companies
-- One row per local install today; the anchor every other table hangs off.
-- Populated by the "First-Time Setup" wizard (Business Type + Payroll toggle
-- + Tax Registration).
-- ---------------------------------------------------------------------------
CREATE TABLE Core.Companies
(
    Id                     UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Companies_Id DEFAULT (NEWSEQUENTIALID()),
    Name                   NVARCHAR(200)    NOT NULL,
    BusinessType           NVARCHAR(20)     NOT NULL CONSTRAINT CK_Companies_BusinessType
                                CHECK (BusinessType IN ('Cafe', 'Trading')),
    PayrollEnabled         BIT              NOT NULL CONSTRAINT DF_Companies_PayrollEnabled DEFAULT (1),
    TaxRegistrationType    NVARCHAR(10)     NOT NULL CONSTRAINT CK_Companies_TaxRegistrationType
                                CHECK (TaxRegistrationType IN ('VAT', 'PAN')),
    VatRegistrationNumber  NVARCHAR(30)     NULL,   -- VAT reg. no. or PAN no., whichever applies
    VatRatePercent         DECIMAL(5,2)     NOT NULL CONSTRAINT DF_Companies_VatRatePercent DEFAULT (13.00),
    PrimaryCurrencyCode    NVARCHAR(3)      NOT NULL CONSTRAINT DF_Companies_Currency DEFAULT ('NPR'),
    CreatedAtUtc           DATETIME2(3)     NOT NULL CONSTRAINT DF_Companies_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId        UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc          DATETIME2(3)     NULL,
    ModifiedByUserId       UNIQUEIDENTIFIER NULL,
    IsDeleted              BIT              NOT NULL CONSTRAINT DF_Companies_IsDeleted DEFAULT (0),
    CONSTRAINT PK_Companies PRIMARY KEY CLUSTERED (Id)
);
GO
GO

-- ---------------------------------------------------------------------------
-- Core.Branches
-- ---------------------------------------------------------------------------
CREATE TABLE Core.Branches
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Branches_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    Name              NVARCHAR(150)    NOT NULL,       -- e.g. "Main Branch — Kathmandu"
    City              NVARCHAR(100)    NULL,
    Address           NVARCHAR(300)    NULL,
    Phone             NVARCHAR(30)     NULL,
    IsPrimary         BIT              NOT NULL CONSTRAINT DF_Branches_IsPrimary DEFAULT (0),
    IsActive          BIT              NOT NULL CONSTRAINT DF_Branches_IsActive DEFAULT (1),
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_Branches_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc     DATETIME2(3)     NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL CONSTRAINT DF_Branches_IsDeleted DEFAULT (0),
    CONSTRAINT PK_Branches PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Branches_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id)
);
GO
CREATE INDEX IX_Branches_CompanyId ON Core.Branches(CompanyId) WHERE IsDeleted = 0;
GO

-- ---------------------------------------------------------------------------
-- Auth.Roles
-- Matches the Settings → Roles & Permissions matrix. Seeded roles (Owner,
-- Manager, Cashier, Accountant, HR) are regular rows, not hardcoded —
-- "New Role" in the UI just inserts one.
-- ---------------------------------------------------------------------------
CREATE TABLE Auth.Roles
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Roles_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    Name              NVARCHAR(100)    NOT NULL,       -- Owner, Manager, Cashier, Accountant, HR, ...
    IsSystemRole      BIT              NOT NULL CONSTRAINT DF_Roles_IsSystemRole DEFAULT (0), -- Owner: not editable/deletable
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_Roles_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc     DATETIME2(3)     NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL CONSTRAINT DF_Roles_IsDeleted DEFAULT (0),
    CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Roles_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT UQ_Roles_Company_Name UNIQUE (CompanyId, Name)
);
GO
GO

-- ---------------------------------------------------------------------------
-- Auth.RolePermissions
-- One row per (Role, Module) — the columns are exactly the matrix shown in
-- Settings: View / Create / Edit / Delete / Approve.
-- ---------------------------------------------------------------------------
CREATE TABLE Auth.RolePermissions
(
    Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_RolePermissions_Id DEFAULT (NEWSEQUENTIALID()),
    RoleId       UNIQUEIDENTIFIER NOT NULL,
    Module       NVARCHAR(40)     NOT NULL CONSTRAINT CK_RolePermissions_Module
                     CHECK (Module IN ('Billing', 'Masters', 'Inventory', 'Transactions',
                                        'Reports', 'Workforce', 'Settings')),
    CanView      BIT NOT NULL CONSTRAINT DF_RolePermissions_CanView   DEFAULT (0),
    CanCreate    BIT NOT NULL CONSTRAINT DF_RolePermissions_CanCreate DEFAULT (0),
    CanEdit      BIT NOT NULL CONSTRAINT DF_RolePermissions_CanEdit   DEFAULT (0),
    CanDelete    BIT NOT NULL CONSTRAINT DF_RolePermissions_CanDelete DEFAULT (0),
    CanApprove   BIT NOT NULL CONSTRAINT DF_RolePermissions_CanApprove DEFAULT (0),
    CONSTRAINT PK_RolePermissions PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_RolePermissions_Role FOREIGN KEY (RoleId) REFERENCES Auth.Roles(Id),
    CONSTRAINT UQ_RolePermissions_Role_Module UNIQUE (RoleId, Module)
);
GO
GO

-- ---------------------------------------------------------------------------
-- Auth.Users
-- EmployeeId is nullable + added as a FK in 07_Workforce_Payroll.sql (that
-- table doesn't exist yet at this point in the run order).
-- ---------------------------------------------------------------------------
CREATE TABLE Auth.Users
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Users_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    RoleId            UNIQUEIDENTIFIER NOT NULL,
    EmployeeId        UNIQUEIDENTIFIER NULL,           -- FK added later, see 07_Workforce_Payroll.sql
    FullName          NVARCHAR(150)    NOT NULL,
    Email             NVARCHAR(200)    NULL,
    Username           NVARCHAR(100)   NOT NULL,
    PasswordHash      NVARCHAR(300)    NOT NULL,
    DefaultBranchId   UNIQUEIDENTIFIER NULL,
    IsActive          BIT              NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
    LastLoginAtUtc    DATETIME2(3)     NULL,
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_Users_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc     DATETIME2(3)     NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL CONSTRAINT DF_Users_IsDeleted DEFAULT (0),
    CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Users_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT FK_Users_Role FOREIGN KEY (RoleId) REFERENCES Auth.Roles(Id),
    CONSTRAINT FK_Users_Branch FOREIGN KEY (DefaultBranchId) REFERENCES Core.Branches(Id),
    CONSTRAINT UQ_Users_Company_Username UNIQUE (CompanyId, Username)
);
GO
CREATE INDEX IX_Users_CompanyId ON Auth.Users(CompanyId) WHERE IsDeleted = 0;
GO
