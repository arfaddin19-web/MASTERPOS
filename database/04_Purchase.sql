/* ============================================================================
   MasterPOS — 04_Purchase.sql
   Purchase Invoices + lines, Purchase Returns + lines.
   Depends on: 01_Core_Auth.sql, 02_Masters.sql
   ============================================================================ */

SET QUOTED_IDENTIFIER ON;
GO

-- ---------------------------------------------------------------------------
-- Purchase.PurchaseInvoices
-- ---------------------------------------------------------------------------
CREATE TABLE Purchase.PurchaseInvoices
(
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PurchaseInvoices_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId           UNIQUEIDENTIFIER NOT NULL,
    BranchId            UNIQUEIDENTIFIER NOT NULL,
    InvoiceNumber       NVARCHAR(30)     NOT NULL,        -- "PO-2214"
    SupplierId          UNIQUEIDENTIFIER NOT NULL,
    SupplierReferenceNo NVARCHAR(50)     NULL,             -- supplier's own invoice number
    InvoiceDate         DATE             NOT NULL,
    PaymentTerms        NVARCHAR(100)    NULL,             -- "Net 15 Days"
    Status               NVARCHAR(20)    NOT NULL CONSTRAINT DF_PurchaseInvoices_Status DEFAULT ('Draft')
                              CONSTRAINT CK_PurchaseInvoices_Status CHECK (Status IN ('Draft', 'Posted', 'Cancelled')),
    SubTotalAmount       DECIMAL(18,2)   NOT NULL CONSTRAINT DF_PurchaseInvoices_SubTotalAmount DEFAULT (0),
    DiscountAmount       DECIMAL(18,2)   NOT NULL CONSTRAINT DF_PurchaseInvoices_DiscountAmount DEFAULT (0),
    VatAmount            DECIMAL(18,2)   NOT NULL CONSTRAINT DF_PurchaseInvoices_VatAmount DEFAULT (0),
    RoundOffAmount       DECIMAL(18,2)   NOT NULL CONSTRAINT DF_PurchaseInvoices_RoundOffAmount DEFAULT (0),
    GrandTotalAmount     DECIMAL(18,2)   NOT NULL CONSTRAINT DF_PurchaseInvoices_GrandTotalAmount DEFAULT (0),
    AmountPaid           DECIMAL(18,2)   NOT NULL CONSTRAINT DF_PurchaseInvoices_AmountPaid DEFAULT (0),
    Narration            NVARCHAR(400)   NULL,
    CreatedAtUtc         DATETIME2(3)    NOT NULL CONSTRAINT DF_PurchaseInvoices_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId      UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc        DATETIME2(3)    NULL,
    ModifiedByUserId     UNIQUEIDENTIFIER NULL,
    IsDeleted             BIT             NOT NULL CONSTRAINT DF_PurchaseInvoices_IsDeleted DEFAULT (0),
    CONSTRAINT PK_PurchaseInvoices PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_PurchaseInvoices_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT FK_PurchaseInvoices_Branch FOREIGN KEY (BranchId) REFERENCES Core.Branches(Id),
    CONSTRAINT FK_PurchaseInvoices_Supplier FOREIGN KEY (SupplierId) REFERENCES Masters.Parties(Id),
    CONSTRAINT UQ_PurchaseInvoices_Company_InvoiceNumber UNIQUE (CompanyId, InvoiceNumber)
);
GO
CREATE INDEX IX_PurchaseInvoices_Company_Status ON Purchase.PurchaseInvoices(CompanyId, Status) WHERE IsDeleted = 0;
GO

-- ---------------------------------------------------------------------------
-- Purchase.PurchaseInvoiceLines
-- ---------------------------------------------------------------------------
CREATE TABLE Purchase.PurchaseInvoiceLines
(
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PurchaseInvoiceLines_Id DEFAULT (NEWSEQUENTIALID()),
    PurchaseInvoiceId   UNIQUEIDENTIFIER NOT NULL,
    ProductId           UNIQUEIDENTIFIER NOT NULL,
    UnitId               UNIQUEIDENTIFIER NOT NULL,
    Quantity             DECIMAL(18,3)   NOT NULL,
    Rate                 DECIMAL(18,2)   NOT NULL,
    DiscountPercent      DECIMAL(5,2)    NOT NULL CONSTRAINT DF_PurchaseInvoiceLines_DiscountPercent DEFAULT (0),
    VatPercent           DECIMAL(5,2)    NOT NULL CONSTRAINT DF_PurchaseInvoiceLines_VatPercent DEFAULT (0),
    LineAmount           DECIMAL(18,2)   NOT NULL,          -- qty*rate, less discount, plus VAT
    CONSTRAINT PK_PurchaseInvoiceLines PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_PurchaseInvoiceLines_Invoice FOREIGN KEY (PurchaseInvoiceId) REFERENCES Purchase.PurchaseInvoices(Id),
    CONSTRAINT FK_PurchaseInvoiceLines_Product FOREIGN KEY (ProductId) REFERENCES Masters.Products(Id),
    CONSTRAINT FK_PurchaseInvoiceLines_Unit FOREIGN KEY (UnitId) REFERENCES Masters.Units(Id)
);
GO
CREATE INDEX IX_PurchaseInvoiceLines_InvoiceId ON Purchase.PurchaseInvoiceLines(PurchaseInvoiceId);
GO

-- ---------------------------------------------------------------------------
-- Purchase.PurchaseReturns / PurchaseReturnLines
-- Mirrors PurchaseInvoices — kept as its own document type (not a negative
-- invoice) so it prints and reports as a distinct transaction, same as the
-- "Purchase Return" tab in the design.
-- ---------------------------------------------------------------------------
CREATE TABLE Purchase.PurchaseReturns
(
    Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PurchaseReturns_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId             UNIQUEIDENTIFIER NOT NULL,
    BranchId              UNIQUEIDENTIFIER NOT NULL,
    ReturnNumber          NVARCHAR(30)     NOT NULL,
    OriginalPurchaseInvoiceId UNIQUEIDENTIFIER NULL,
    SupplierId            UNIQUEIDENTIFIER NOT NULL,
    ReturnDate            DATE             NOT NULL,
    Status                NVARCHAR(20)     NOT NULL CONSTRAINT DF_PurchaseReturns_Status DEFAULT ('Draft')
                               CONSTRAINT CK_PurchaseReturns_Status CHECK (Status IN ('Draft', 'Posted', 'Cancelled')),
    SubTotalAmount        DECIMAL(18,2)    NOT NULL CONSTRAINT DF_PurchaseReturns_SubTotalAmount DEFAULT (0),
    VatAmount             DECIMAL(18,2)    NOT NULL CONSTRAINT DF_PurchaseReturns_VatAmount DEFAULT (0),
    GrandTotalAmount      DECIMAL(18,2)    NOT NULL CONSTRAINT DF_PurchaseReturns_GrandTotalAmount DEFAULT (0),
    Narration              NVARCHAR(400)   NULL,
    CreatedAtUtc           DATETIME2(3)    NOT NULL CONSTRAINT DF_PurchaseReturns_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId        UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc           DATETIME2(3)   NULL,
    ModifiedByUserId        UNIQUEIDENTIFIER NULL,
    IsDeleted               BIT            NOT NULL CONSTRAINT DF_PurchaseReturns_IsDeleted DEFAULT (0),
    CONSTRAINT PK_PurchaseReturns PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_PurchaseReturns_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT FK_PurchaseReturns_Branch FOREIGN KEY (BranchId) REFERENCES Core.Branches(Id),
    CONSTRAINT FK_PurchaseReturns_OriginalInvoice FOREIGN KEY (OriginalPurchaseInvoiceId) REFERENCES Purchase.PurchaseInvoices(Id),
    CONSTRAINT FK_PurchaseReturns_Supplier FOREIGN KEY (SupplierId) REFERENCES Masters.Parties(Id),
    CONSTRAINT UQ_PurchaseReturns_Company_ReturnNumber UNIQUE (CompanyId, ReturnNumber)
);
GO
GO

CREATE TABLE Purchase.PurchaseReturnLines
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PurchaseReturnLines_Id DEFAULT (NEWSEQUENTIALID()),
    PurchaseReturnId  UNIQUEIDENTIFIER NOT NULL,
    ProductId         UNIQUEIDENTIFIER NOT NULL,
    UnitId            UNIQUEIDENTIFIER NOT NULL,
    Quantity          DECIMAL(18,3)    NOT NULL,
    Rate              DECIMAL(18,2)    NOT NULL,
    VatPercent        DECIMAL(5,2)     NOT NULL CONSTRAINT DF_PurchaseReturnLines_VatPercent DEFAULT (0),
    LineAmount        DECIMAL(18,2)    NOT NULL,
    CONSTRAINT PK_PurchaseReturnLines PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_PurchaseReturnLines_Return FOREIGN KEY (PurchaseReturnId) REFERENCES Purchase.PurchaseReturns(Id),
    CONSTRAINT FK_PurchaseReturnLines_Product FOREIGN KEY (ProductId) REFERENCES Masters.Products(Id),
    CONSTRAINT FK_PurchaseReturnLines_Unit FOREIGN KEY (UnitId) REFERENCES Masters.Units(Id)
);
GO
CREATE INDEX IX_PurchaseReturnLines_ReturnId ON Purchase.PurchaseReturnLines(PurchaseReturnId);
GO
