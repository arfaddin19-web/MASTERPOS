# MasterPOS — Database Schema (MSSQL)

Derived directly from the finalized design (`MasterPOS Design Suite`). Target: **SQL Server 2019+** (Express edition is fine for a single local install).

## Deployment model this schema is built for

- One SQL Server instance per client, installed locally on the client's "main" PC (SQL Server Express). All other devices (POS terminal, waiter tablets, kitchen display) reach the app over the client's own Wi-Fi/LAN — no internet dependency for day-to-day billing.
- **Every business table carries a `CompanyId`.** A local install only ever has one row in `Core.Companies`, but this column is what makes the later move to SaaS (whether "one isolated hosted instance per client" or a shared multi-tenant database) a data migration, not a schema rewrite.
- **Primary keys are `UNIQUEIDENTIFIER` (GUID), not `IDENTITY` integers**, generated with `NEWSEQUENTIALID()`. This is the other half of the SaaS-readiness story: if several clients' local databases are ever merged into one (backup consolidation, migrating everyone to one cloud database), GUID keys never collide. `NEWSEQUENTIALID()` keeps them roughly increasing so indexes don't fragment the way random GUIDs would.

## Conventions

- **Schemas** mirror the app's own modules (`Core`, `Auth`, `Masters`, `Sales`, `Purchase`, `Accounting`, `Inventory`, `Workforce`, `Utility`) — keeps a large database navigable in SSMS instead of one flat `dbo` bucket.
- **Naming**: PascalCase for schemas, tables, columns — matches C# entity classes 1:1 for EF Core, no mapping-attribute noise.
- **Types**: `DECIMAL(18,2)` for money (Rs.), `DECIMAL(5,2)` for percentages/rates, `DECIMAL(18,3)` for quantities (fractional units like kg), `BIT` for booleans, `DATETIME2(3)` for timestamps (stored UTC), `DATE` for date-only fields, `NVARCHAR` everywhere text can include Nepali/Devanagari script.
- **Fixed-choice fields** (order type, payment mode, status, etc.) use `NVARCHAR` + `CHECK` constraints rather than a separate lookup table for every small enum — readable directly in SSMS, which matters for after-sale support.
- **Every table** carries the same four audit columns plus a soft-delete flag: `CreatedAtUtc`, `CreatedByUserId`, `ModifiedAtUtc`, `ModifiedByUserId`, `IsDeleted`. `CreatedByUserId`/`ModifiedByUserId` are plain GUIDs, deliberately **not** foreign-keyed to `Auth.Users` — avoids a circular bootstrap problem and leaves room for a system/seed user.
- Run the files in numeric order — later files reference earlier ones by foreign key.

## Files

| File | Schema(s) | Covers |
|---|---|---|
| `01_Core_Auth.sql` | Core, Auth | Companies (business profile from the Setup wizard), Branches, Roles, RolePermissions, Users |
| `02_Masters.sql` | Masters | Product Category/Group/Unit/Warehouse/Product/BOM, Dining Tables, Party (supplier/customer unified), Loyalty & Discount masters, Chart of Accounts |
| `03_Sales_POS.sql` | Sales | Orders, OrderLines (with per-item KOT note + station), OrderPayments (the Split Payment multi-tender ledger), KOT print log |
| `04_Purchase.sql` | Purchase | Purchase Invoices + lines, Purchase Returns + lines |
| `05_Accounting.sql` | Accounting | Journal Entries + lines, Party Payments (Payment Entry transaction), Opening Balances |
| `06_Inventory.sql` | Inventory | Stock Ledger (running balance), Stock Adjustment, Stock Transfer, Opening Stock |
| `07_Workforce_Payroll.sql` | Workforce | Employees, Attendance, Leave, Advances, Payroll Runs + lines |
| `08_Utilities.sql` | Utility | Printers, Payment Modes, Backup Log, Audit Log |

## Business-type conditionality (from the Setup wizard)

`Core.Companies.BusinessType` (`Cafe` or `Trading`) and `PayrollEnabled` don't hide columns — every table always has the full column set. The application layer reads `Companies.BusinessType`/`PayrollEnabled` and decides which fields/screens/modules to show:
- `Masters.Products.Barcode` is populated and used for **Trading**; `Masters.Products.KotStation`/`PrepTimeMinutes` are populated and used for **Cafe**. Both columns exist regardless — a business can switch types later without a migration.
- `Masters.DiningTables` / `Sales.Orders.TableId` / the whole Live Floor Status story only applies when `BusinessType = 'Cafe'`.
- The entire `07_Workforce_Payroll.sql` module is only exposed in the UI when `Companies.PayrollEnabled = 1` — the tables exist either way so turning it on later needs no migration.

## Tax (VAT/PAN) — from the Nepal tax rules

`Core.Companies.TaxRegistrationType` (`VAT` or `PAN`) and `VatRegistrationNumber` are set once, company-wide — never per bill. `Masters.Products.IsVatApplicable` is the only per-product tax choice (tick/untick, no percentage picker — Nepal's VAT is a flat rate stored once in `Companies.VatRatePercent`, defaulted to `13.00`).
