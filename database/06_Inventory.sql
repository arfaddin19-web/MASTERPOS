/* ============================================================================
   MasterPOS — 06_Inventory.sql
   Stock Ledger (running balance, feeds the Stock Register / Item Ledger
   reports), Stock Adjustment, Stock Transfer, Opening Stock.
   Depends on: 01_Core_Auth.sql, 02_Masters.sql
   ============================================================================ */

SET QUOTED_IDENTIFIER ON;
GO

-- ---------------------------------------------------------------------------
-- Inventory.StockLedgerEntries
-- Append-only. Every stock-moving transaction (Purchase, PurchaseReturn,
-- an Order closing, Adjustment, Transfer, Opening Stock) writes exactly one
-- row here per product/warehouse affected — this table is the single
-- source of truth for "closing stock", never a value stored on Products.
-- ---------------------------------------------------------------------------
CREATE TABLE Inventory.StockLedgerEntries
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_StockLedgerEntries_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    WarehouseId       UNIQUEIDENTIFIER NOT NULL,
    ProductId         UNIQUEIDENTIFIER NOT NULL,
    MovementDate      DATE             NOT NULL,
    QuantityIn        DECIMAL(18,3)    NOT NULL CONSTRAINT DF_StockLedgerEntries_QuantityIn DEFAULT (0),
    QuantityOut       DECIMAL(18,3)    NOT NULL CONSTRAINT DF_StockLedgerEntries_QuantityOut DEFAULT (0),
    ReferenceType     NVARCHAR(30)     NOT NULL CONSTRAINT CK_StockLedgerEntries_ReferenceType
                          CHECK (ReferenceType IN ('PurchaseInvoice', 'PurchaseReturn', 'Order', 'Adjustment', 'TransferOut', 'TransferIn', 'OpeningStock')),
    ReferenceId       UNIQUEIDENTIFIER NOT NULL,
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_StockLedgerEntries_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    CONSTRAINT PK_StockLedgerEntries PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_StockLedgerEntries_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT FK_StockLedgerEntries_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Masters.Warehouses(Id),
    CONSTRAINT FK_StockLedgerEntries_Product FOREIGN KEY (ProductId) REFERENCES Masters.Products(Id)
);
GO
CREATE INDEX IX_StockLedgerEntries_Product_Warehouse_Date ON Inventory.StockLedgerEntries(ProductId, WarehouseId, MovementDate);
GO

-- ---------------------------------------------------------------------------
-- Inventory.StockAdjustments
-- Single-item corrections (breakage, count mismatch, expiry write-off).
-- Posting one writes a matching Inventory.StockLedgerEntries row.
-- ---------------------------------------------------------------------------
CREATE TABLE Inventory.StockAdjustments
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_StockAdjustments_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    WarehouseId       UNIQUEIDENTIFIER NOT NULL,
    ProductId         UNIQUEIDENTIFIER NOT NULL,
    QuantityChange    DECIMAL(18,3)    NOT NULL,          -- positive = found extra stock, negative = write-off
    Reason            NVARCHAR(200)    NOT NULL,
    AdjustmentDate    DATE             NOT NULL,
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_StockAdjustments_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc     DATETIME2(3)     NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL CONSTRAINT DF_StockAdjustments_IsDeleted DEFAULT (0),
    CONSTRAINT PK_StockAdjustments PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_StockAdjustments_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT FK_StockAdjustments_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Masters.Warehouses(Id),
    CONSTRAINT FK_StockAdjustments_Product FOREIGN KEY (ProductId) REFERENCES Masters.Products(Id)
);
GO
GO

-- ---------------------------------------------------------------------------
-- Inventory.StockTransfers
-- Matches the "Quick Stock Transfer" form — one product, one From/To
-- warehouse pair. Posting one writes a TransferOut row at the source and a
-- TransferIn row at the destination.
-- ---------------------------------------------------------------------------
CREATE TABLE Inventory.StockTransfers
(
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_StockTransfers_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId           UNIQUEIDENTIFIER NOT NULL,
    ProductId           UNIQUEIDENTIFIER NOT NULL,
    FromWarehouseId     UNIQUEIDENTIFIER NOT NULL,
    ToWarehouseId       UNIQUEIDENTIFIER NOT NULL,
    Quantity            DECIMAL(18,3)    NOT NULL CONSTRAINT CK_StockTransfers_Quantity CHECK (Quantity > 0),
    TransferDate        DATE             NOT NULL,
    Status              NVARCHAR(20)     NOT NULL CONSTRAINT DF_StockTransfers_Status DEFAULT ('Completed')
                             CONSTRAINT CK_StockTransfers_Status CHECK (Status IN ('Pending', 'Completed', 'Cancelled')),
    CreatedAtUtc         DATETIME2(3)    NOT NULL CONSTRAINT DF_StockTransfers_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId      UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc        DATETIME2(3)    NULL,
    ModifiedByUserId     UNIQUEIDENTIFIER NULL,
    IsDeleted            BIT             NOT NULL CONSTRAINT DF_StockTransfers_IsDeleted DEFAULT (0),
    CONSTRAINT PK_StockTransfers PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_StockTransfers_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT FK_StockTransfers_Product FOREIGN KEY (ProductId) REFERENCES Masters.Products(Id),
    CONSTRAINT FK_StockTransfers_FromWarehouse FOREIGN KEY (FromWarehouseId) REFERENCES Masters.Warehouses(Id),
    CONSTRAINT FK_StockTransfers_ToWarehouse FOREIGN KEY (ToWarehouseId) REFERENCES Masters.Warehouses(Id),
    CONSTRAINT CK_StockTransfers_DifferentWarehouses CHECK (FromWarehouseId <> ToWarehouseId)
);
GO
GO

-- ---------------------------------------------------------------------------
-- Inventory.OpeningStock
-- ---------------------------------------------------------------------------
CREATE TABLE Inventory.OpeningStock
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_OpeningStock_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    WarehouseId       UNIQUEIDENTIFIER NOT NULL,
    ProductId         UNIQUEIDENTIFIER NOT NULL,
    Quantity          DECIMAL(18,3)    NOT NULL,
    UnitCost          DECIMAL(18,2)    NOT NULL CONSTRAINT DF_OpeningStock_UnitCost DEFAULT (0),
    AsOfDate          DATE             NOT NULL,
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_OpeningStock_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc     DATETIME2(3)     NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL CONSTRAINT DF_OpeningStock_IsDeleted DEFAULT (0),
    CONSTRAINT PK_OpeningStock PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_OpeningStock_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT FK_OpeningStock_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Masters.Warehouses(Id),
    CONSTRAINT FK_OpeningStock_Product FOREIGN KEY (ProductId) REFERENCES Masters.Products(Id),
    CONSTRAINT UQ_OpeningStock_Warehouse_Product UNIQUE (WarehouseId, ProductId)
);
GO
GO
