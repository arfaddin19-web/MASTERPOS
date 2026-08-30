/* ============================================================================
   MasterPOS — 07_Workforce_Payroll.sql
   Employees, Attendance, Leave, Advances, Payroll Runs + lines.
   These tables exist regardless of Companies.PayrollEnabled — the app just
   hides the module in the UI when it's off, so turning it on later needs
   no migration.
   Depends on: 01_Core_Auth.sql
   ============================================================================ */

SET QUOTED_IDENTIFIER ON;
GO

-- ---------------------------------------------------------------------------
-- Workforce.Employees
-- ---------------------------------------------------------------------------
CREATE TABLE Workforce.Employees
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Employees_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    BranchId          UNIQUEIDENTIFIER NOT NULL,
    FullName          NVARCHAR(150)    NOT NULL,
    RoleTitle         NVARCHAR(100)    NULL,             -- "Store Manager", "Cashier", ... (job title, not Auth.Roles)
    Phone             NVARCHAR(30)     NULL,
    JoinDate          DATE             NOT NULL,
    BasicSalary       DECIMAL(18,2)    NOT NULL CONSTRAINT DF_Employees_BasicSalary DEFAULT (0),
    ShiftStart        TIME(0)          NULL,
    ShiftEnd          TIME(0)          NULL,
    MaritalStatus     NVARCHAR(10)     NOT NULL CONSTRAINT DF_Employees_MaritalStatus DEFAULT ('Single')
                          CONSTRAINT CK_Employees_MaritalStatus CHECK (MaritalStatus IN ('Single', 'Couple')),
    IsActive          BIT              NOT NULL CONSTRAINT DF_Employees_IsActive DEFAULT (1),
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_Employees_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc     DATETIME2(3)     NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL CONSTRAINT DF_Employees_IsDeleted DEFAULT (0),
    CONSTRAINT PK_Employees PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Employees_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT FK_Employees_Branch FOREIGN KEY (BranchId) REFERENCES Core.Branches(Id)
);
GO
GO

-- Now that Workforce.Employees exists, wire up the FK left open in 01_Core_Auth.sql.
ALTER TABLE Auth.Users ADD CONSTRAINT FK_Users_Employee FOREIGN KEY (EmployeeId) REFERENCES Workforce.Employees(Id);
GO

-- ---------------------------------------------------------------------------
-- Workforce.Attendance
-- ---------------------------------------------------------------------------
CREATE TABLE Workforce.Attendance
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Attendance_Id DEFAULT (NEWSEQUENTIALID()),
    EmployeeId        UNIQUEIDENTIFIER NOT NULL,
    AttendanceDate    DATE             NOT NULL,
    CheckInAtUtc      DATETIME2(3)     NULL,
    CheckOutAtUtc     DATETIME2(3)     NULL,
    Status            NVARCHAR(20)     NOT NULL CONSTRAINT CK_Attendance_Status
                          CHECK (Status IN ('Present', 'Late', 'Absent', 'OnLeave')),
    OvertimeHours     DECIMAL(5,2)     NOT NULL CONSTRAINT DF_Attendance_OvertimeHours DEFAULT (0),
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_Attendance_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc     DATETIME2(3)     NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL CONSTRAINT DF_Attendance_IsDeleted DEFAULT (0),
    CONSTRAINT PK_Attendance PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Attendance_Employee FOREIGN KEY (EmployeeId) REFERENCES Workforce.Employees(Id),
    CONSTRAINT UQ_Attendance_Employee_Date UNIQUE (EmployeeId, AttendanceDate)
);
GO
GO

-- ---------------------------------------------------------------------------
-- Workforce.LeaveRequests
-- ---------------------------------------------------------------------------
CREATE TABLE Workforce.LeaveRequests
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_LeaveRequests_Id DEFAULT (NEWSEQUENTIALID()),
    EmployeeId        UNIQUEIDENTIFIER NOT NULL,
    LeaveType         NVARCHAR(30)     NOT NULL,        -- Sick, Casual, Annual, Unpaid, ...
    FromDate          DATE             NOT NULL,
    ToDate            DATE             NOT NULL,
    Status            NVARCHAR(20)     NOT NULL CONSTRAINT DF_LeaveRequests_Status DEFAULT ('Pending')
                          CONSTRAINT CK_LeaveRequests_Status CHECK (Status IN ('Pending', 'Approved', 'Rejected')),
    ApprovedByUserId  UNIQUEIDENTIFIER NULL,
    Reason            NVARCHAR(300)    NULL,
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_LeaveRequests_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc     DATETIME2(3)     NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL CONSTRAINT DF_LeaveRequests_IsDeleted DEFAULT (0),
    CONSTRAINT PK_LeaveRequests PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_LeaveRequests_Employee FOREIGN KEY (EmployeeId) REFERENCES Workforce.Employees(Id),
    CONSTRAINT FK_LeaveRequests_ApprovedBy FOREIGN KEY (ApprovedByUserId) REFERENCES Auth.Users(Id),
    CONSTRAINT CK_LeaveRequests_DateOrder CHECK (ToDate >= FromDate)
);
GO
GO

-- ---------------------------------------------------------------------------
-- Workforce.EmployeeAdvances
-- ---------------------------------------------------------------------------
CREATE TABLE Workforce.EmployeeAdvances
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_EmployeeAdvances_Id DEFAULT (NEWSEQUENTIALID()),
    EmployeeId        UNIQUEIDENTIFIER NOT NULL,
    Amount            DECIMAL(18,2)    NOT NULL CONSTRAINT CK_EmployeeAdvances_Amount CHECK (Amount > 0),
    AdvanceDate       DATE             NOT NULL,
    Reason            NVARCHAR(300)    NULL,
    AmountRecovered   DECIMAL(18,2)    NOT NULL CONSTRAINT DF_EmployeeAdvances_AmountRecovered DEFAULT (0),
    Status            NVARCHAR(20)     NOT NULL CONSTRAINT DF_EmployeeAdvances_Status DEFAULT ('Open')
                          CONSTRAINT CK_EmployeeAdvances_Status CHECK (Status IN ('Open', 'PartiallyRecovered', 'Recovered')),
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_EmployeeAdvances_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc     DATETIME2(3)     NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL CONSTRAINT DF_EmployeeAdvances_IsDeleted DEFAULT (0),
    CONSTRAINT PK_EmployeeAdvances PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_EmployeeAdvances_Employee FOREIGN KEY (EmployeeId) REFERENCES Workforce.Employees(Id)
);
GO
GO

-- ---------------------------------------------------------------------------
-- Workforce.PayrollRuns / PayrollRunLines
-- One run per period; one line per employee, computed from Attendance +
-- EmployeeAdvances + manual allowance/deduction entries.
-- ---------------------------------------------------------------------------
CREATE TABLE Workforce.PayrollRuns
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PayrollRuns_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    BranchId          UNIQUEIDENTIFIER NOT NULL,
    PeriodMonth       TINYINT          NOT NULL CONSTRAINT CK_PayrollRuns_PeriodMonth CHECK (PeriodMonth BETWEEN 1 AND 12),
    PeriodYear        SMALLINT         NOT NULL,
    RunType           NVARCHAR(20)     NOT NULL CONSTRAINT DF_PayrollRuns_RunType DEFAULT ('Monthly')
                          CONSTRAINT CK_PayrollRuns_RunType CHECK (RunType IN ('Monthly', 'FestivalBonus')),
    Status            NVARCHAR(20)     NOT NULL CONSTRAINT DF_PayrollRuns_Status DEFAULT ('Draft')
                          CONSTRAINT CK_PayrollRuns_Status CHECK (Status IN ('Draft', 'Completed')),
    RunAtUtc          DATETIME2(3)     NULL,
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_PayrollRuns_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc     DATETIME2(3)     NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL CONSTRAINT DF_PayrollRuns_IsDeleted DEFAULT (0),
    CONSTRAINT PK_PayrollRuns PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_PayrollRuns_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT FK_PayrollRuns_Branch FOREIGN KEY (BranchId) REFERENCES Core.Branches(Id),
    -- One run per Branch/Month/Year *per RunType* — a Monthly run and a
    -- FestivalBonus run for the same period are different documents, not
    -- two attempts at the same one.
    CONSTRAINT UQ_PayrollRuns_Branch_Period_Type UNIQUE (BranchId, PeriodYear, PeriodMonth, RunType)
);
GO
GO

CREATE TABLE Workforce.PayrollRunLines
(
    Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PayrollRunLines_Id DEFAULT (NEWSEQUENTIALID()),
    PayrollRunId          UNIQUEIDENTIFIER NOT NULL,
    EmployeeId            UNIQUEIDENTIFIER NOT NULL,
    BasicAmount           DECIMAL(18,2)    NOT NULL,
    AllowancesAmount      DECIMAL(18,2)    NOT NULL CONSTRAINT DF_PayrollRunLines_AllowancesAmount DEFAULT (0),
    OvertimeAmount        DECIMAL(18,2)    NOT NULL CONSTRAINT DF_PayrollRunLines_OvertimeAmount DEFAULT (0),
    DeductionsAmount      DECIMAL(18,2)    NOT NULL CONSTRAINT DF_PayrollRunLines_DeductionsAmount DEFAULT (0),
    AdvanceDeductionAmount DECIMAL(18,2)   NOT NULL CONSTRAINT DF_PayrollRunLines_AdvanceDeductionAmount DEFAULT (0),
    PfEmployeeAmount      DECIMAL(18,2)    NOT NULL CONSTRAINT DF_PayrollRunLines_PfEmployeeAmount DEFAULT (0),   -- reduces NetPayAmount
    PfEmployerAmount      DECIMAL(18,2)    NOT NULL CONSTRAINT DF_PayrollRunLines_PfEmployerAmount DEFAULT (0),   -- informational only
    SsfEmployeeAmount     DECIMAL(18,2)    NOT NULL CONSTRAINT DF_PayrollRunLines_SsfEmployeeAmount DEFAULT (0),  -- reduces NetPayAmount
    SsfEmployerAmount     DECIMAL(18,2)    NOT NULL CONSTRAINT DF_PayrollRunLines_SsfEmployerAmount DEFAULT (0),  -- informational only
    TdsAmount             DECIMAL(18,2)    NOT NULL CONSTRAINT DF_PayrollRunLines_TdsAmount DEFAULT (0),          -- reduces NetPayAmount
    NetPayAmount          DECIMAL(18,2)    NOT NULL,
    LineStatus            NVARCHAR(30)     NOT NULL CONSTRAINT DF_PayrollRunLines_LineStatus DEFAULT ('Ready')
                               CONSTRAINT CK_PayrollRunLines_LineStatus CHECK (LineStatus IN ('Ready', 'LeaveDeduction', 'AttendancePending')),
    CONSTRAINT PK_PayrollRunLines PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_PayrollRunLines_Run FOREIGN KEY (PayrollRunId) REFERENCES Workforce.PayrollRuns(Id),
    CONSTRAINT FK_PayrollRunLines_Employee FOREIGN KEY (EmployeeId) REFERENCES Workforce.Employees(Id),
    CONSTRAINT UQ_PayrollRunLines_Run_Employee UNIQUE (PayrollRunId, EmployeeId)
);
GO
GO

-- ---------------------------------------------------------------------------
-- Workforce.PayrollSettings
-- One row per Company — the Payroll Settings screen (OT / PF / SSF / TDS /
-- Festival Bonus toggles). PayrollRunService reads this live at compute
-- time; nothing here is a hardcoded constant in application code.
-- PF and SSF are independent toggles, not mutually exclusive at the schema
-- level — real Nepali practice normally registers under one scheme or the
-- other, never both, but that's a business decision left to the company.
-- ---------------------------------------------------------------------------
CREATE TABLE Workforce.PayrollSettings
(
    Id                     UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PayrollSettings_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId              UNIQUEIDENTIFIER NOT NULL,
    OvertimeEnabled        BIT              NOT NULL CONSTRAINT DF_PayrollSettings_OvertimeEnabled DEFAULT (1),
    OvertimeMultiplier     DECIMAL(5,2)     NOT NULL CONSTRAINT DF_PayrollSettings_OvertimeMultiplier DEFAULT (1.5)
                               CONSTRAINT CK_PayrollSettings_OvertimeMultiplier CHECK (OvertimeMultiplier >= 0),
    PfEnabled              BIT              NOT NULL CONSTRAINT DF_PayrollSettings_PfEnabled DEFAULT (0),
    PfEmployeePercent      DECIMAL(5,2)     NOT NULL CONSTRAINT DF_PayrollSettings_PfEmployeePercent DEFAULT (10),
    PfEmployerPercent      DECIMAL(5,2)     NOT NULL CONSTRAINT DF_PayrollSettings_PfEmployerPercent DEFAULT (10),
    SsfEnabled             BIT              NOT NULL CONSTRAINT DF_PayrollSettings_SsfEnabled DEFAULT (0),
    SsfEmployeePercent     DECIMAL(5,2)     NOT NULL CONSTRAINT DF_PayrollSettings_SsfEmployeePercent DEFAULT (11),
    SsfEmployerPercent     DECIMAL(5,2)     NOT NULL CONSTRAINT DF_PayrollSettings_SsfEmployerPercent DEFAULT (20),
    TdsEnabled             BIT              NOT NULL CONSTRAINT DF_PayrollSettings_TdsEnabled DEFAULT (0),
    FestivalBonusEnabled   BIT              NOT NULL CONSTRAINT DF_PayrollSettings_FestivalBonusEnabled DEFAULT (0),
    FestivalBonusPercent   DECIMAL(5,2)     NOT NULL CONSTRAINT DF_PayrollSettings_FestivalBonusPercent DEFAULT (100),
    CreatedAtUtc           DATETIME2(3)     NOT NULL CONSTRAINT DF_PayrollSettings_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    ModifiedAtUtc          DATETIME2(3)     NULL,
    IsDeleted              BIT              NOT NULL CONSTRAINT DF_PayrollSettings_IsDeleted DEFAULT (0),
    CONSTRAINT PK_PayrollSettings PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_PayrollSettings_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT UQ_PayrollSettings_Company UNIQUE (CompanyId),
    CONSTRAINT CK_PayrollSettings_PfPercents CHECK (PfEmployeePercent >= 0 AND PfEmployerPercent >= 0),
    CONSTRAINT CK_PayrollSettings_SsfPercents CHECK (SsfEmployeePercent >= 0 AND SsfEmployerPercent >= 0),
    CONSTRAINT CK_PayrollSettings_FestivalBonusPercent CHECK (FestivalBonusPercent >= 0)
);
GO
GO

-- ---------------------------------------------------------------------------
-- Workforce.TaxSlabs
-- One row per band of Nepal's progressive individual income-tax table.
-- The government revises thresholds and rates almost every fiscal year, so
-- this is a company-editable table, not a constant in code — seeded with a
-- commonly-cited recent structure at first use, which the admin should
-- verify against the current official rates before relying on it.
-- ---------------------------------------------------------------------------
CREATE TABLE Workforce.TaxSlabs
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_TaxSlabs_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    MaritalStatus     NVARCHAR(10)     NOT NULL CONSTRAINT CK_TaxSlabs_MaritalStatus CHECK (MaritalStatus IN ('Single', 'Couple')),
    LowerBound        DECIMAL(18,2)    NOT NULL,          -- annual taxable income, inclusive
    UpperBound        DECIMAL(18,2)    NULL,              -- inclusive; NULL = no upper bound (top band)
    RatePercent       DECIMAL(5,2)     NOT NULL CONSTRAINT CK_TaxSlabs_RatePercent CHECK (RatePercent BETWEEN 0 AND 100),
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_TaxSlabs_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    ModifiedAtUtc     DATETIME2(3)     NULL,
    IsDeleted         BIT              NOT NULL CONSTRAINT DF_TaxSlabs_IsDeleted DEFAULT (0),
    CONSTRAINT PK_TaxSlabs PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_TaxSlabs_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT CK_TaxSlabs_Bounds CHECK (UpperBound IS NULL OR UpperBound > LowerBound)
);
CREATE INDEX IX_TaxSlabs_Company_Status_Lower ON Workforce.TaxSlabs (CompanyId, MaritalStatus, LowerBound);
GO
GO
