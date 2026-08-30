/* ============================================================================
   MasterPOS — 05_Accounting.sql
   Journal Entries + lines, Party Payments ("Payment Entry" transaction —
   distinct from Sales.OrderPayments, which is POS/table money), Opening
   Balances (party or ledger account).
   Depends on: 01_Core_Auth.sql, 02_Masters.sql, 04_Purchase.sql
   ============================================================================ */

SET QUOTED_IDENTIFIER ON;
GO

-- ---------------------------------------------------------------------------
-- Accounting.JournalEntries / JournalEntryLines
-- Standard double-entry journal — every line is a Debit or a Credit against
-- one Chart of Accounts row; the app enforces SUM(Debit) = SUM(Credit)
-- before allowing a JournalEntry to post.
-- ---------------------------------------------------------------------------
CREATE TABLE Accounting.JournalEntries
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_JournalEntries_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    BranchId          UNIQUEIDENTIFIER NOT NULL,
    JournalNumber     NVARCHAR(30)     NOT NULL,        -- "JE-0442"
    EntryDate         DATE             NOT NULL,
    Narration         NVARCHAR(400)    NULL,
    Status            NVARCHAR(20)     NOT NULL CONSTRAINT DF_JournalEntries_Status DEFAULT ('Draft')
                          CONSTRAINT CK_JournalEntries_Status CHECK (Status IN ('Draft', 'Posted', 'Cancelled')),
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_JournalEntries_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc     DATETIME2(3)     NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL CONSTRAINT DF_JournalEntries_IsDeleted DEFAULT (0),
    CONSTRAINT PK_JournalEntries PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_JournalEntries_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT FK_JournalEntries_Branch FOREIGN KEY (BranchId) REFERENCES Core.Branches(Id),
    CONSTRAINT UQ_JournalEntries_Company_JournalNumber UNIQUE (CompanyId, JournalNumber)
);
GO
GO

CREATE TABLE Accounting.JournalEntryLines
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_JournalEntryLines_Id DEFAULT (NEWSEQUENTIALID()),
    JournalEntryId    UNIQUEIDENTIFIER NOT NULL,
    AccountId         UNIQUEIDENTIFIER NOT NULL,
    DebitAmount       DECIMAL(18,2)    NOT NULL CONSTRAINT DF_JournalEntryLines_DebitAmount DEFAULT (0),
    CreditAmount      DECIMAL(18,2)    NOT NULL CONSTRAINT DF_JournalEntryLines_CreditAmount DEFAULT (0),
    LineNarration     NVARCHAR(300)    NULL,
    CONSTRAINT PK_JournalEntryLines PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_JournalEntryLines_Entry FOREIGN KEY (JournalEntryId) REFERENCES Accounting.JournalEntries(Id),
    CONSTRAINT FK_JournalEntryLines_Account FOREIGN KEY (AccountId) REFERENCES Masters.ChartOfAccounts(Id),
    CONSTRAINT CK_JournalEntryLines_OneSided CHECK (
        (DebitAmount > 0 AND CreditAmount = 0) OR (CreditAmount > 0 AND DebitAmount = 0)
    )
);
GO
CREATE INDEX IX_JournalEntryLines_EntryId ON Accounting.JournalEntryLines(JournalEntryId);
CREATE INDEX IX_JournalEntryLines_AccountId ON Accounting.JournalEntryLines(AccountId);
GO

-- ---------------------------------------------------------------------------
-- Accounting.PartyPayments
-- The "Payment Entry" transaction type — settling a party's outstanding
-- balance (a supplier bill, or a customer's running account), independent
-- of a specific POS order. ReferenceType/ReferenceId optionally ties a
-- payment to the invoice it's settling.
-- ---------------------------------------------------------------------------
CREATE TABLE Accounting.PartyPayments
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PartyPayments_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    BranchId          UNIQUEIDENTIFIER NOT NULL,
    PartyId           UNIQUEIDENTIFIER NOT NULL,
    Direction         NVARCHAR(10)     NOT NULL CONSTRAINT CK_PartyPayments_Direction
                          CHECK (Direction IN ('Paid', 'Received')),      -- Paid = to a supplier, Received = from a customer
    Amount            DECIMAL(18,2)    NOT NULL CONSTRAINT CK_PartyPayments_Amount CHECK (Amount > 0),
    PaymentMode       NVARCHAR(20)     NOT NULL CONSTRAINT CK_PartyPayments_PaymentMode
                          CHECK (PaymentMode IN ('Cash', 'Card', 'eSewa', 'Khalti', 'BankTransfer')),
    ReferenceType     NVARCHAR(30)     NULL CONSTRAINT CK_PartyPayments_ReferenceType
                          CHECK (ReferenceType IS NULL OR ReferenceType IN ('PurchaseInvoice', 'PurchaseReturn', 'OpeningBalance')),
    ReferenceId       UNIQUEIDENTIFIER NULL,
    PaymentDate       DATE             NOT NULL,
    Narration         NVARCHAR(400)    NULL,
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_PartyPayments_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc     DATETIME2(3)     NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL CONSTRAINT DF_PartyPayments_IsDeleted DEFAULT (0),
    CONSTRAINT PK_PartyPayments PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_PartyPayments_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT FK_PartyPayments_Branch FOREIGN KEY (BranchId) REFERENCES Core.Branches(Id),
    CONSTRAINT FK_PartyPayments_Party FOREIGN KEY (PartyId) REFERENCES Masters.Parties(Id)
);
GO
CREATE INDEX IX_PartyPayments_PartyId ON Accounting.PartyPayments(PartyId) WHERE IsDeleted = 0;
GO

-- ---------------------------------------------------------------------------
-- Accounting.OpeningBalances
-- The "Opening Balance (Party, Accounts)" transaction — one row per party
-- OR per ledger account (never both), so it shows as its own transaction
-- in reports rather than being silently baked into a master record.
-- ---------------------------------------------------------------------------
CREATE TABLE Accounting.OpeningBalances
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_OpeningBalances_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    PartyId           UNIQUEIDENTIFIER NULL,
    AccountId         UNIQUEIDENTIFIER NULL,
    Amount            DECIMAL(18,2)    NOT NULL,
    BalanceType       NVARCHAR(2)      NOT NULL CONSTRAINT CK_OpeningBalances_BalanceType CHECK (BalanceType IN ('Dr', 'Cr')),
    AsOfDate          DATE             NOT NULL,
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_OpeningBalances_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc     DATETIME2(3)     NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL CONSTRAINT DF_OpeningBalances_IsDeleted DEFAULT (0),
    CONSTRAINT PK_OpeningBalances PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_OpeningBalances_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT FK_OpeningBalances_Party FOREIGN KEY (PartyId) REFERENCES Masters.Parties(Id),
    CONSTRAINT FK_OpeningBalances_Account FOREIGN KEY (AccountId) REFERENCES Masters.ChartOfAccounts(Id),
    CONSTRAINT CK_OpeningBalances_ExactlyOneTarget CHECK (
        (CASE WHEN PartyId IS NOT NULL THEN 1 ELSE 0 END
       + CASE WHEN AccountId IS NOT NULL THEN 1 ELSE 0 END) = 1
    )
);
GO
GO
