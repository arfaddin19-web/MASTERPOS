/* ============================================================================
   MasterPOS — 02_Masters.sql
   Product Category/Group/Unit/Warehouse/Product/BOM, Dining Tables,
   Party (supplier/customer unified), Loyalty & Discount masters,
   Chart of Accounts.
   Depends on: 01_Core_Auth.sql
   ============================================================================ */

SET QUOTED_IDENTIFIER ON;
GO

-- ---------------------------------------------------------------------------
-- Masters.ProductCategories / ProductGroups / Units
-- Each has the "+ quick add" affordance in the Product form — plain lookup
-- tables, nothing fancier needed.
-- ---------------------------------------------------------------------------
CREATE TABLE Masters.ProductCategories
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ProductCategories_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    Name              NVARCHAR(100)    NOT NULL,
    ParentCategoryId  UNIQUEIDENTIFIER NULL,           -- self-reference, optional sub-categories
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_ProductCategories_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc     DATETIME2(3)     NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL CONSTRAINT DF_ProductCategories_IsDeleted DEFAULT (0),
    CONSTRAINT PK_ProductCategories PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_ProductCategories_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT FK_ProductCategories_Parent FOREIGN KEY (ParentCategoryId) REFERENCES Masters.ProductCategories(Id),
    CONSTRAINT UQ_ProductCategories_Company_Name UNIQUE (CompanyId, Name)
);
GO
GO

CREATE TABLE Masters.ProductGroups
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ProductGroups_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    Name              NVARCHAR(100)    NOT NULL,
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_ProductGroups_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc     DATETIME2(3)     NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL CONSTRAINT DF_ProductGroups_IsDeleted DEFAULT (0),
    CONSTRAINT PK_ProductGroups PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_ProductGroups_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT UQ_ProductGroups_Company_Name UNIQUE (CompanyId, Name)
);
GO
GO

CREATE TABLE Masters.Units
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Units_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    Name              NVARCHAR(50)     NOT NULL,       -- Bag, Bottle, Piece, Plate, Glass, Kg...
    ShortCode         NVARCHAR(10)     NULL,
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_Units_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc     DATETIME2(3)     NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL CONSTRAINT DF_Units_IsDeleted DEFAULT (0),
    CONSTRAINT PK_Units PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Units_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT UQ_Units_Company_Name UNIQUE (CompanyId, Name)
);
GO
GO

-- ---------------------------------------------------------------------------
-- Masters.Warehouses
-- ---------------------------------------------------------------------------
CREATE TABLE Masters.Warehouses
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Warehouses_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    BranchId          UNIQUEIDENTIFIER NOT NULL,
    Name              NVARCHAR(150)    NOT NULL,       -- Main Warehouse, Lalitpur Branch, ...
    IsDefault         BIT              NOT NULL CONSTRAINT DF_Warehouses_IsDefault DEFAULT (0),
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_Warehouses_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc     DATETIME2(3)     NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL CONSTRAINT DF_Warehouses_IsDeleted DEFAULT (0),
    CONSTRAINT PK_Warehouses PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Warehouses_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT FK_Warehouses_Branch FOREIGN KEY (BranchId) REFERENCES Core.Branches(Id)
);
GO
GO

-- ---------------------------------------------------------------------------
-- Masters.Products
-- Barcode and KotStation/PrepTimeMinutes both exist regardless of
-- BusinessType — the app shows/uses whichever set applies (see README).
--
-- ProductType drives which fields/tabs the app shows and how a sale affects
-- stock:
--   Inventory   — a stocked item: sellable directly at POS/Billing, and/or
--                 usable as a Masters.ProductBom ingredient of a Recipe
--                 item (rice, dal, a bottle of Coke, a packet of chips).
--   Service     — a non-stock line (delivery charge, table service fee).
--                 Never has stock, a Warehouse, or a BOM.
--   Recipe      — a composite/menu item built from a BOM of Inventory
--                 components (Veg Thali, Cappuccino). Selling one deducts
--                 every BOM component from stock instead of itself.
--   Consumable  — stocked for internal operational use only: never sold,
--                 never a BOM ingredient (thermal paper rolls, stationery,
--                 cleaning supplies, packaging).
-- ---------------------------------------------------------------------------
CREATE TABLE Masters.Products
(
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Products_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId           UNIQUEIDENTIFIER NOT NULL,
    CategoryId          UNIQUEIDENTIFIER NULL,
    GroupId             UNIQUEIDENTIFIER NULL,
    UnitId              UNIQUEIDENTIFIER NOT NULL,
    DefaultWarehouseId  UNIQUEIDENTIFIER NULL,
    Name                NVARCHAR(200)    NOT NULL,
    ProductType         NVARCHAR(20)     NOT NULL CONSTRAINT DF_Products_ProductType DEFAULT ('Inventory')
                             CONSTRAINT CK_Products_ProductType
                             CHECK (ProductType IN ('Inventory', 'Service', 'Recipe', 'Consumable')),
    Barcode             NVARCHAR(50)     NULL,           -- Trading: scanned at POS
    PurchasePrice       DECIMAL(18,2)    NOT NULL CONSTRAINT DF_Products_PurchasePrice DEFAULT (0),
    SalePrice           DECIMAL(18,2)    NOT NULL CONSTRAINT DF_Products_SalePrice DEFAULT (0),
    IsVatApplicable     BIT              NOT NULL CONSTRAINT DF_Products_IsVatApplicable DEFAULT (1), -- tick/untick only, rate lives on Companies.VatRatePercent
    ReorderLevel        DECIMAL(18,3)    NOT NULL CONSTRAINT DF_Products_ReorderLevel DEFAULT (0),
    KotStation          NVARCHAR(20)     NULL CONSTRAINT CK_Products_KotStation
                             CHECK (KotStation IS NULL OR KotStation IN ('Kitchen', 'Bar')),  -- Cafe: routes the KOT
    PrepTimeMinutes     INT              NULL,            -- Cafe: estimated prep time
    ImagePath           NVARCHAR(400)    NULL,             -- most products won't have one — UI keeps this compact
    TrackInPos          BIT              NOT NULL CONSTRAINT DF_Products_TrackInPos DEFAULT (1),
    IsActive            BIT              NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT (1),
    CreatedAtUtc        DATETIME2(3)     NOT NULL CONSTRAINT DF_Products_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId     UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc       DATETIME2(3)     NULL,
    ModifiedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted           BIT              NOT NULL CONSTRAINT DF_Products_IsDeleted DEFAULT (0),
    CONSTRAINT PK_Products PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Products_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT FK_Products_Category FOREIGN KEY (CategoryId) REFERENCES Masters.ProductCategories(Id),
    CONSTRAINT FK_Products_Group FOREIGN KEY (GroupId) REFERENCES Masters.ProductGroups(Id),
    CONSTRAINT FK_Products_Unit FOREIGN KEY (UnitId) REFERENCES Masters.Units(Id),
    CONSTRAINT FK_Products_Warehouse FOREIGN KEY (DefaultWarehouseId) REFERENCES Masters.Warehouses(Id)
);
GO
CREATE INDEX IX_Products_CompanyId ON Masters.Products(CompanyId) WHERE IsDeleted = 0;
CREATE UNIQUE INDEX UQ_Products_Company_Barcode ON Masters.Products(CompanyId, Barcode) WHERE Barcode IS NOT NULL AND IsDeleted = 0;
GO

-- ---------------------------------------------------------------------------
-- Masters.ProductBom
-- Recipe/composite items — e.g. a "Veg Thali" made of several stocked
-- ingredients, so selling one deducts each component from stock.
-- ---------------------------------------------------------------------------
CREATE TABLE Masters.ProductBom
(
    Id                 UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ProductBom_Id DEFAULT (NEWSEQUENTIALID()),
    FinishedProductId  UNIQUEIDENTIFIER NOT NULL,
    ComponentProductId UNIQUEIDENTIFIER NOT NULL,
    Quantity           DECIMAL(18,3)    NOT NULL,
    CreatedAtUtc       DATETIME2(3)     NOT NULL CONSTRAINT DF_ProductBom_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc      DATETIME2(3)     NULL,
    ModifiedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL CONSTRAINT DF_ProductBom_IsDeleted DEFAULT (0),
    CONSTRAINT PK_ProductBom PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_ProductBom_Finished FOREIGN KEY (FinishedProductId) REFERENCES Masters.Products(Id),
    CONSTRAINT FK_ProductBom_Component FOREIGN KEY (ComponentProductId) REFERENCES Masters.Products(Id),
    CONSTRAINT CK_ProductBom_NotSelf CHECK (FinishedProductId <> ComponentProductId)
);
GO
GO

-- ---------------------------------------------------------------------------
-- Masters.DiningTables
-- Cafe/Restaurant only (app hides this whole area for Trading). Status is
-- kept here (not just derived) so the floor plan and the Dashboard's Live
-- Floor Status card are cheap to query; the app updates it whenever an
-- order opens, gets a partial payment, or closes.
-- ---------------------------------------------------------------------------
CREATE TABLE Masters.DiningTables
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_DiningTables_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    BranchId          UNIQUEIDENTIFIER NOT NULL,
    TableNumber       NVARCHAR(20)     NOT NULL,        -- "01", "11", ...
    FloorLabel        NVARCHAR(50)     NULL,             -- "Ground Floor", "First Floor"
    Seats             INT              NOT NULL CONSTRAINT DF_DiningTables_Seats DEFAULT (4),
    Status            NVARCHAR(20)     NOT NULL CONSTRAINT DF_DiningTables_Status DEFAULT ('Vacant')
                          CONSTRAINT CK_DiningTables_Status CHECK (Status IN ('Vacant', 'Occupied', 'PartiallyPaid')),
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_DiningTables_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc     DATETIME2(3)     NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL CONSTRAINT DF_DiningTables_IsDeleted DEFAULT (0),
    CONSTRAINT PK_DiningTables PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_DiningTables_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT FK_DiningTables_Branch FOREIGN KEY (BranchId) REFERENCES Core.Branches(Id),
    CONSTRAINT UQ_DiningTables_Branch_Number UNIQUE (BranchId, TableNumber)
);
GO
CREATE INDEX IX_DiningTables_Branch_Status ON Masters.DiningTables(BranchId, Status) WHERE IsDeleted = 0;
GO

-- ---------------------------------------------------------------------------
-- Masters.Parties
-- Unifies "Party Master" and "Customer Master" from the design — PartyType
-- says which the record is (a Customer just carries loyalty fields the
-- application only reads when PartyType includes 'Customer').
-- ---------------------------------------------------------------------------
CREATE TABLE Masters.Parties
(
    Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Parties_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId             UNIQUEIDENTIFIER NOT NULL,
    PartyType             NVARCHAR(20)     NOT NULL CONSTRAINT CK_Parties_PartyType
                               CHECK (PartyType IN ('Supplier', 'Customer', 'Both')),
    Name                  NVARCHAR(200)    NOT NULL,
    Phone                 NVARCHAR(30)     NULL,
    Email                 NVARCHAR(200)    NULL,
    Address               NVARCHAR(300)    NULL,
    VatOrPanNumber        NVARCHAR(30)     NULL,
    OpeningBalanceAmount  DECIMAL(18,2)    NOT NULL CONSTRAINT DF_Parties_OpeningBalanceAmount DEFAULT (0),
    OpeningBalanceType    NVARCHAR(2)      NOT NULL CONSTRAINT DF_Parties_OpeningBalanceType DEFAULT ('Dr')
                               CONSTRAINT CK_Parties_OpeningBalanceType CHECK (OpeningBalanceType IN ('Dr', 'Cr')),
    LoyaltyPoints         INT              NOT NULL CONSTRAINT DF_Parties_LoyaltyPoints DEFAULT (0),  -- Customer only
    IsActive              BIT              NOT NULL CONSTRAINT DF_Parties_IsActive DEFAULT (1),
    CreatedAtUtc          DATETIME2(3)     NOT NULL CONSTRAINT DF_Parties_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId       UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc         DATETIME2(3)     NULL,
    ModifiedByUserId      UNIQUEIDENTIFIER NULL,
    IsDeleted             BIT              NOT NULL CONSTRAINT DF_Parties_IsDeleted DEFAULT (0),
    CONSTRAINT PK_Parties PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Parties_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id)
);
GO
CREATE INDEX IX_Parties_Company_Type ON Masters.Parties(CompanyId, PartyType) WHERE IsDeleted = 0;
GO

-- ---------------------------------------------------------------------------
-- Masters.DiscountOffers
-- ---------------------------------------------------------------------------
CREATE TABLE Masters.DiscountOffers
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_DiscountOffers_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    Name              NVARCHAR(150)    NOT NULL,
    DiscountType      NVARCHAR(10)     NOT NULL CONSTRAINT CK_DiscountOffers_DiscountType
                          CHECK (DiscountType IN ('Percent', 'Amount')),
    Value             DECIMAL(18,2)    NOT NULL,
    ValidFrom         DATE             NULL,
    ValidTo           DATE             NULL,
    IsActive          BIT              NOT NULL CONSTRAINT DF_DiscountOffers_IsActive DEFAULT (1),
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_DiscountOffers_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc     DATETIME2(3)     NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL CONSTRAINT DF_DiscountOffers_IsDeleted DEFAULT (0),
    CONSTRAINT PK_DiscountOffers PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_DiscountOffers_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id)
);
GO
GO

-- ---------------------------------------------------------------------------
-- Masters.ChartOfAccounts
-- Backs the Ledger and Final Account reports (Trial Balance, P&L,
-- Balance Sheet).
-- ---------------------------------------------------------------------------
CREATE TABLE Masters.ChartOfAccounts
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ChartOfAccounts_Id DEFAULT (NEWSEQUENTIALID()),
    CompanyId         UNIQUEIDENTIFIER NOT NULL,
    ParentAccountId   UNIQUEIDENTIFIER NULL,
    Name              NVARCHAR(150)    NOT NULL,
    AccountType       NVARCHAR(20)     NOT NULL CONSTRAINT CK_ChartOfAccounts_AccountType
                          CHECK (AccountType IN ('Asset', 'Liability', 'Equity', 'Income', 'Expense')),
    IsSystemAccount   BIT              NOT NULL CONSTRAINT DF_ChartOfAccounts_IsSystemAccount DEFAULT (0), -- e.g. Cash, VAT Payable — created by the app, not deletable
    CreatedAtUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_ChartOfAccounts_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedAtUtc     DATETIME2(3)     NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL CONSTRAINT DF_ChartOfAccounts_IsDeleted DEFAULT (0),
    CONSTRAINT PK_ChartOfAccounts PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_ChartOfAccounts_Company FOREIGN KEY (CompanyId) REFERENCES Core.Companies(Id),
    CONSTRAINT FK_ChartOfAccounts_Parent FOREIGN KEY (ParentAccountId) REFERENCES Masters.ChartOfAccounts(Id)
);
GO
GO
