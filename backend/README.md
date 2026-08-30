# MasterPOS Backend (.NET 8)

The API for MasterPOS — a unified POS + ERP + Payroll system for Nepal-market
cafes and trading businesses. Deployed once per client, on their own machine,
against their own local SQL Server (see **Deployment model** below).

This backend implements the schema in `../database/` exactly, via EF Core.
The database scripts in that folder are the authoritative source of truth
for column types, lengths, and constraints — the C# model was written to
match them, not the other way around, and every piece of it has been
validated against a real SQL Server instance (see **How this was verified**).

## Architecture

Four projects, each depending only on the one to its left:

```
Domain  ←  Infrastructure  ←  Application  ←  Api
```

- **`MasterPOS.Domain`** — plain C# entity classes (POCOs) and enums. No EF
  Core reference, no attributes tying it to a database. `AuditableEntity` /
  `CompanyOwnedEntity` in `Common/` are the base classes every entity
  extends; `Common/Enums.cs` holds every enum, each matching a raw-SQL
  `CHECK (... IN (...))` value list.

- **`MasterPOS.Infrastructure`** — `MasterPosDbContext`, one
  `IEntityTypeConfiguration<T>` class per entity (in
  `Persistence/Configurations/`, one file per schema/module), and the EF
  Core migrations. This is the only project that knows about EF Core or SQL
  Server. Every table's Fluent API configuration includes the same `CHECK`
  constraints, defaults, indexes, and foreign keys as the matching raw SQL
  file — see **Keeping this in sync with the raw SQL** below.

- **`MasterPOS.Application`** — business logic, as one folder per
  module (`Auth/`, `Setup/`, and so on as more are added). Each module gets
  an interface + implementation registered in `DependencyInjection.cs`.
  Talks to `MasterPosDbContext` directly (no repository abstraction — this
  is a pragmatic layering, not textbook Clean Architecture).

- **`MasterPOS.Api`** — ASP.NET Core Web API. Thin controllers that call
  into Application services and translate `AppException` into a 4xx
  response. `Program.cs` wires up EF Core, JWT auth, and Swagger.

## The reference vertical slices: Auth + Setup, Masters, Sales, Purchase, Inventory, Workforce, Accounting, Reports, and Utility

Ten modules are fully implemented end-to-end, as the pattern for
everything else:

- **Setup** (`Application/Setup/`, `Api/Controllers/SetupController.cs`) —
  the First-Time Setup wizard from the design phase. `GET /api/setup/status`
  tells the client whether to show the wizard or the login screen;
  `POST /api/setup` creates the Company, its primary Branch, an "Admin"
  role with full rights on every module, and the first admin user — all in
  one transaction, and only once (a second call is rejected).

- **Auth** (`Application/Auth/`, `Api/Controllers/AuthController.cs`) —
  `POST /api/auth/login` verifies username + password
  (`Microsoft.Extensions.Identity.Core`'s `PasswordHasher<User>` — the same
  PBKDF2-based hasher ASP.NET Core Identity uses, without pulling in
  Identity's own schema), issues a JWT carrying the user's id, role, company,
  and default branch, and returns the caller's full permission matrix
  (`Module` × `CanView`/`CanCreate`/`CanEdit`/`CanDelete`/`CanApprove`) so
  the client can build its UI without a second call.

  **Roles & Users** (`Api/Controllers/RolesController.cs` +
  `UsersController.cs`) round out Auth beyond the one Admin login Setup
  creates: `Role` CRUD requires a permission entry for every
  `PermissionModule` exactly once (missing or duplicate modules are
  rejected, 400) and blocks editing/deleting the seeded system `Admin`
  role outright; `User` CRUD creates additional logins against any
  non-system role, and is deactivate-only by design — `PATCH .../active`,
  never `DELETE` — since too much of the schema references `Users.Id`
  (`CreatedByUserId`, `ApprovedByUserId`, `CashierUserId`, ...) for a hard
  delete to ever be safe. You can't deactivate your own account (400), a
  deactivated user's login is rejected (401) even with the right
  password, and `.../reset-password` re-hashes without needing the old one.

- **Masters** (`Application/Masters/`, `Api/Controllers/ProductsController.cs`
  + `MastersLookupsController.cs`) — the first module behind `[Authorize]`,
  so it's also where `ICurrentUserContext` (below) gets used for real.
  Full CRUD on `Products` (including the `ProductType` field and the
  Recipe/BOM builder from the Masters design screen), plus the four
  "+ quick-add" lookups (Category, Group, Unit, Warehouse). The BOM
  endpoints enforce the rules the Masters design screen implies but can't
  itself enforce: `GET/PUT /api/masters/products/{id}/bom` only works on a
  `Recipe`-type product, every ingredient must itself be `Inventory`-type
  (no sub-recipes, no Service/Consumable ingredients), and deleting a
  product that's still used as an ingredient is rejected (409) rather than
  silently orphaning the recipe.

  **Once a product has any transaction history — an Order/Purchase line, a
  stock movement of any kind — its fields are frozen.** `PUT .../{id}`
  (full edit) and `DELETE .../{id}` both check every transaction-bearing
  table (`OrderLines`, `PurchaseInvoiceLines`, `PurchaseReturnLines`,
  `StockLedgerEntries`, `StockAdjustments`, `StockTransfers`,
  `OpeningStocks`) and reject with 409 the moment any of them reference the
  product — a past document's line ("sold 2× Rice at Rs. 400") must keep
  meaning exactly what it said at the time, not silently reprice itself
  because the product master changed later. The one edit still allowed
  after that point is `PATCH .../{id}/active` (`SetProductActiveRequest
  { IsActive }`) — deactivating changes nothing a past transaction
  depended on, so it's always permitted. A product with no transaction
  history yet can still be fully edited *or* deleted, same as before.

  **Parties** (`Api/Controllers/PartiesController.cs`) — unifies Customer
  and Supplier master data (`PartyType`: `Supplier`/`Customer`/`Both`),
  with the exact same transaction-lock shape as Products: once a Party
  has any `PurchaseInvoice`/`PurchaseReturn`/`Order` referencing it,
  `PUT`/`DELETE` are rejected (409) and only `PATCH .../active` remains.
  **Dining Tables** (`Api/Controllers/DiningTablesController.cs`) — the
  Cafe-only floor plan Sales already reads/writes `TableId`/table status
  against; this service only owns the table's own identity (number,
  floor, seats) and refuses to edit or delete one that isn't currently
  `Vacant` — Sales owns the `Vacant`/`Occupied`/`PartiallyPaid`
  transition itself as orders open, part-pay, and close, so this never
  fights it for control of that field.

- **Sales** (`Application/Sales/`, `Api/Controllers/OrdersController.cs`) —
  the POS billing flow: `POST /api/sales/orders` opens an order (Dine-in
  seats it at a vacant Table and marks the table Occupied); `POST/PUT/DELETE
  .../{id}/lines` add/adjust/remove items (rejecting `Consumable` products
  outright, and anything not `TrackInPos`/`IsActive` — the exact rule the
  POS design check asked for); `POST .../{id}/kot` sends only the lines
  still `Pending` per station, marking a second send `IsReprint`; `POST
  .../{id}/payments` is the Split Payment screen's one entry point — each
  call updates the order's status (`Open` → `PartiallyPaid` → `Paid`) and
  keeps the linked Table's status in lock-step (`Occupied` →
  `PartiallyPaid` → `Vacant`); `POST .../{id}/hold` and `.../{id}/cancel`
  round it out.

  **Closing an order (reaching `Paid`) is the one moment stock actually
  moves** — matching `06_Inventory.sql`'s own comment that "an Order
  closing" is a stock-moving transaction. This is where the whole
  `ProductType`/BOM story from the design checks earlier in this project
  actually executes: an `Inventory` line deducts itself; a `Recipe` line
  deducts its BOM components instead, each scaled by the line's quantity
  (2× Veg Thali → 0.4 kg Rice + 0.3 kg Dal out, not "2× Veg Thali" out —
  verified against a live database, not just asserted); a `Service` line
  never touches stock. Every line snapshots its `UnitPrice` and
  `KotStation` from the product at add-time, for the same reason Masters'
  transaction lock exists — a closed order's line must keep meaning what
  it said when it was rung up.

  Order lines can only be added/changed while an order is
  `Open`/`PartiallyPaid`/`OnHold` — a `Paid` or `Cancelled` order is frozen,
  the same principle as the Masters transaction lock applied to the
  document itself. `OrderNumber` ("ORD-10231") is generated by finding the
  highest existing numeric suffix for the company and incrementing — fine
  for a single local install with one cashier terminal at a time, not
  designed for concurrent-writer races.

  **Discount Offers** (`Api/Controllers/DiscountOffersController.cs`,
  applied via `POST .../orders/{id}/discount/offer` — a saved offer — or
  `.../discount/manual` — an ad-hoc one-off — and cleared with `DELETE
  .../discount`) round out billing. An offer is rejected (400) if it's
  inactive or outside its `ValidFrom`/`ValidTo` window; a flat `Amount`
  discount is capped at the bill's own subtotal so it can never make the
  bill negative. Applying one *fixed a real bug* in the original totals
  formula: `RecalculateTotalsAsync` had always computed VAT on the full
  pre-discount vatable amount and subtracted the discount only afterward
  — never actually exercised before, since `DiscountAmount` was always 0
  until this pass. VAT is properly charged on the *discounted* taxable
  value now — the order-level `DiscountAmount` is prorated across the
  bill's vatable/non-vatable portions before VAT is computed on the
  now-smaller vatable base, the same discount-then-VAT order Purchase's
  own per-line math already used. With `DiscountAmount = 0` the new
  formula is numerically identical to the old one, so nothing already
  verified changed — only the discount path, which had never been
  exercised, actually got more correct.

- **Purchase** (`Application/Purchase/`, `Api/Controllers/PurchaseInvoicesController.cs`
  + `PurchaseReturnsController.cs`) — the buy-side counterpart to Sales,
  covering the Purchase design screen's two documents:

  - **Purchase Invoice**: `POST /api/purchase/invoices` opens a `Draft`
    against a `Supplier`-type Party; `POST/PUT/DELETE .../{id}/lines`
    build it up (rejecting `Recipe`/`Service` products — you can't buy a
    composed dish or a non-stock service from a supplier, only
    `Inventory`/`Consumable` items); line math matches the original design
    mockup's own worked example exactly: `qty × rate`, less a line discount
    %, plus VAT % on the discounted taxable amount, each line rounded to 2
    decimals, then subtotal/discount/VAT/round-off rolled up the same way
    at the document level. `POST .../{id}/post` is the one-way transition
    that actually moves stock — one `StockLedgerEntry` per line
    (`QuantityIn`, `ReferenceType = PurchaseInvoice`) — after which the
    invoice is frozen (`PUT`/lines all rejected, same transaction-lock
    principle as Masters and Sales) and only `.../{id}/cancel` (Draft only)
    or a Purchase Return remain. `POST .../{id}/payments` records
    supplier payments against `AmountPaid` independent of posting.
  - **Purchase Return**: the reversal path — `POST /api/purchase/returns`
    optionally references the original posted invoice (rejected if that
    invoice isn't `Posted` yet); same Draft → line-build → `.../post` shape,
    but posting writes `QuantityOut` (`ReferenceType = PurchaseReturn`)
    instead, giving suppliers/stock an honest paper trail for what actually
    left again rather than editing the original invoice's history away.

  Both documents share the same `Draft`/`Posted`/`Cancelled` lifecycle and
  the same "posting is the only moment stock moves" rule Sales established
  for Orders — `06_Inventory.sql`'s comment about exactly-one-row-per-
  stock-moving-transaction now holds for Purchase Invoice, Purchase Return,
  *and* Order all three, from three independently-built modules converging
  on the same rule because the schema requires it, not because the code
  was copy-pasted.

- **Inventory** (`Application/Inventory/`, `Api/Controllers/StockAdjustmentsController.cs`
  + `StockTransfersController.cs` + `OpeningStockController.cs` +
  `StockReportsController.cs`) — everything that isn't a Purchase or a
  Sale but still moves the same `StockLedgerEntries` table:

  - **Opening Stock** (`POST /api/inventory/opening-stock`) — the one-time
    starting balance per product/warehouse. No Draft/Posted step at all:
    creating one *is* posting it, in the same call, because the domain
    entity itself carries no status field to stage it with. The unique
    (Warehouse, Product) pair — one opening balance per product per
    warehouse, ever — is enforced both here (a friendly 400 before the
    insert) and by the database's own unique index; correcting a mistake
    afterward goes through a Stock Adjustment, not a rewrite.
  - **Stock Adjustment** (`POST /api/inventory/adjustments`) — single-item
    corrections (breakage, a count mismatch, an expiry write-off). Same
    "creating it posts it" shape as Opening Stock, for the same reason
    (`StockAdjustment` has no status column either): positive quantity
    change writes `QuantityIn`, negative writes `QuantityOut`, both
    `ReferenceType = Adjustment`.
  - **Stock Transfer** (`POST /api/inventory/transfers`, `.../{id}/post`,
    `.../{id}/cancel`) — the one document in this module that *does* have
    a Status column (`Pending`/`Completed`/`Cancelled`), so it's the one
    that follows the Draft→Posted shape Purchase and Sales already
    established: `Create` always starts `Pending` — regardless of the
    entity's own `Completed` CLR default, which exists only as a sane
    fallback for a direct DB insert, not for anything the Application
    layer does — and moves nothing yet; `.../post` is where the two
    ledger rows actually get written (`TransferOut` at the source,
    `TransferIn` at the destination) and where the source warehouse's
    current balance is checked first, rejecting the post outright if
    there isn't enough stock to move rather than letting it go negative
    silently; a still-`Pending` transfer can be `.../cancel`led with
    nothing to reverse, since nothing moved yet.
  - **Stock reports** (`GET /api/inventory/reports/ledger`, `.../balances`,
    `.../reorder-suggestions`) — read-only, nothing here ever writes to
    `StockLedgerEntries`. `ledger` is the Stock Register / Item Ledger
    screen: every entry for a product/warehouse with a running balance
    computed across the *entire* history first, with any `fromDate`/
    `toDate` filter applied only after that running total is built — a
    date filter narrows what's *shown*, it can never reset the balance to
    zero at an arbitrary point. `balances` is the same running total, just
    the final number per product/warehouse instead of the full trail.
    `reorder-suggestions` compares each active product's `ReorderLevel`
    (a single company-wide setting on the product, not one per warehouse)
    against its stock balance summed across every warehouse, returning
    only the ones at or below that level.

  All three document types share the same product-type gate Purchase's
  `ValidatePurchasableProductAsync` uses: only `Inventory`/`Consumable`
  products hold their own stock, so a `Recipe` or `Service` product is
  rejected (400) the moment it's used here — the same rule, independently
  re-enforced a third time by a third module because the schema requires
  it, not because the check was copy-pasted in.

- **Workforce** (`Application/Workforce/`, `Api/Controllers/EmployeesController.cs`
  + `AttendanceController.cs` + `LeaveRequestsController.cs` +
  `EmployeeAdvancesController.cs` + `PayrollRunsController.cs`) — the
  "Payroll" in MasterPOS, covering the Payroll tab's "Run Payroll" button
  and the three feeder screens (Attendance, Leave, Employees) behind it:

  - **Employee** — full CRUD, but *without* Masters' transaction-lock rule:
    a salary raise or a shift change is normal HR business even after
    payroll history exists, and every `PayrollRunLine` already snapshots
    its own `BasicAmount` at run time, so a later edit can't silently
    rewrite a past run. Only `DELETE` is still blocked (409) once the
    employee has any Attendance, Leave, Advance, or Payroll history —
    `PATCH .../active` (deactivate) is always available instead, and a
    deactivated employee is simply skipped by every future payroll run.
    `MaritalStatus` (`Single`/`Couple`) lives here too — it's the one
    per-employee input the TDS calculation needs, since Nepal's income-tax
    slabs use a wider band for a Couple.
  - **Attendance** — `POST .../check-in` (creates today's row, `Late` if
    past `ShiftStart` + 15 minutes grace), `POST .../{id}/check-out` (computes
    `OvertimeHours` from the employee's own shift length, 0 if no shift is
    set), and `POST .../mark` for manual back-office correction — an
    upsert against the (Employee, Date) unique index rather than a second
    call failing on it. `GET .../today` is the "Today's Attendance
    Snapshot" card: every active employee in the caller's branch, whether
    or not they've been marked yet, with a not-yet-marked employee on an
    *approved* leave for today showing `OnLeave` even with no Attendance
    row at all — read from `LeaveRequests` directly, not from Attendance.
  - **Leave** — `Pending` → `Approve`/`Reject` (recording the deciding
    user), or the requester's own `Cancel` while still `Pending`; once
    decided, a leave request is a historical record, not editable.
  - **Employee Advance** — `Amount`/`AmountRecovered`/`Status` age
    normally (`Open` → `PartiallyRecovered` → `Recovered`), recoverable
    either manually (`POST .../{id}/recover`, capped at the outstanding
    balance) or automatically by a payroll run's own advance deduction.
  - **Payroll Settings** (`GET`/`PUT /api/workforce/payroll-settings`) —
    one row per company, auto-created with sensible defaults (Overtime on
    at 1.5×, everything else off) the first time anyone asks for it — no
    explicit "initialize" step. Every toggle here is a business decision
    the company makes once and `PayrollRunService` reads live at compute
    time: `OvertimeEnabled`/`OvertimeMultiplier`, `PfEnabled` with
    separate employee/employer %, `SsfEnabled` with separate employee/
    employer %, `TdsEnabled`, and `FestivalBonusEnabled`/
    `FestivalBonusPercent`. PF and SSF are independent toggles here, not
    mutually exclusive — real Nepali practice registers a company under
    one scheme or the other, never both, but that's the company's own
    decision, not something the schema enforces.
  - **Tax Slabs** (`GET`/`POST`/`PUT`/`DELETE /api/workforce/tax-slabs`,
    `POST .../seed-defaults`) — Nepal's progressive individual income-tax
    table, company-editable rather than a constant in code, since the
    government revises thresholds and rates almost every fiscal year.
    Each row is one band: `MaritalStatus`, `LowerBound`/`UpperBound`
    (annual taxable income, `UpperBound` null on the top band), and
    `RatePercent`. `.../seed-defaults` loads a commonly-cited recent
    structure (separate Single/Couple tables, 1%/10%/20%/30%/36% bands) —
    a starting point for the admin to verify against the current official
    rates before relying on it, not a guarantee of them, and it refuses to
    run a second time once any slab exists so it can never silently
    clobber an admin's own edits. Creating or editing a slab is rejected
    (400) if its range overlaps another slab already configured for the
    same marital status — a bad config here would silently mis-tax every
    employee under it.
  - **Payroll Run** — `POST /api/workforce/payroll-runs` *is* "Run
    Payroll": one call creates the Draft run for a Branch + calendar month
    + `RunType` (`Monthly` or `FestivalBonus`, default `Monthly`) and
    computes every active employee's line together — one run per
    Branch/Month/Year/RunType (rejecting a duplicate outright; a Monthly
    and a FestivalBonus run for the same period coexist as different
    documents). A `Monthly` run, per employee: `BasicAmount` is the
    monthly salary minus a per-day rate for each `Absent` day in the
    period; `OvertimeAmount` is `OvertimeHours` from Attendance at
    `PayrollSettings.OvertimeMultiplier`, zero outright when
    `OvertimeEnabled` is off; `PfEmployeeAmount`/`PfEmployerAmount` and
    `SsfEmployeeAmount`/`SsfEmployerAmount` are each % of `BasicAmount`
    from Payroll Settings, zero when their own toggle is off (only the
    employee-side amounts ever reduce `NetPayAmount` — the employer-side
    ones are informational, for statutory filing, never subtracted from
    the employee's own pay); `TdsAmount` — only when `TdsEnabled` —
    annualizes this month's taxable pay (basic + overtime, less the PF/SSF
    employee contributions, which are pre-tax deductible under Nepali law)
    ×12, walks the employee's own `TaxSlabs` band-by-band for their
    `MaritalStatus`, and divides the resulting annual tax back down to a
    monthly figure — the standard small-business simplification (assumes
    this month's pay repeats all year), not a cumulative year-to-date
    withholding calculation, documented as such in `PayrollRunService`
    rather than presented as more precise than it is; `AdvanceDeductionAmount`
    is the employee's outstanding advance balance, capped at that line's
    own net-after-statutory-deductions pay so a line can never go
    negative. `LineStatus` reads exactly like the design's own badges:
    `AttendancePending` when the period doesn't have as many marked days
    yet as it should (the full month once it's over, day-of-month while
    it's still current), `LeaveDeduction` when it's fully marked but
    includes an absence, `Ready` otherwise. A `FestivalBonus` run skips
    all of that — `.../payroll-runs` rejects creating one at all unless
    `FestivalBonusEnabled` is on — and computes one line per employee as
    `BasicSalary × FestivalBonusPercent`, carried in `AllowancesAmount`
    since a bonus is exactly that, an allowance, not wages; no OT/PF/SSF/
    TDS/attendance logic applies to it at all. Either run type: `Draft`
    can be freely `.../recompute`d as attendance/advance/settings data
    changes; `.../complete` is the one-way step that locks it and actually
    walks each line's `AdvanceDeductionAmount` back into the employee's
    advance record(s), oldest-first.

- **Accounting** (`Application/Accounting/`, `Api/Controllers/
  ChartOfAccountsController.cs` + `JournalEntriesController.cs` +
  `PartyPaymentsController.cs` + `OpeningBalancesController.cs`) — manual
  double-entry bookkeeping underneath Purchase/Sales, not (yet) auto-fed
  by them:
  - **Chart of Accounts** — CRUD plus `.../seed-defaults` (Cash, Bank,
    Accounts Receivable/Payable, VAT Payable, Opening Balance Equity,
    Sales Revenue, Purchases/COGS — marked `IsSystemAccount`, protected
    from edit/delete, only when the company has none yet), self-
    referencing `ParentAccountId` for sub-accounts.
  - **Journal Entries** — `Draft` → line-build → `.../post`, same shape as
    every other document here, but Post's real job is enforcing the one
    rule a single-row CHECK constraint can't: total debits must equal
    total credits across the *whole* entry (400, naming both totals, if
    not) — on top of each line already being one-sided (exactly one of
    Debit/Credit, validated before it ever reaches
    `CK_JournalEntryLines_OneSided`).
  - **Party Payments** — the "Payment Entry" transaction, settling a
    party's balance independent of any specific order. Immutable once
    recorded (Create/Get/List only, no edit/delete — same principle as
    `StockLedgerEntry`/`OrderPayment`). A `PurchaseInvoice` reference
    advances that invoice's own `AmountPaid` too (capped at its remaining
    balance, the exact same check `PurchaseInvoiceService.
    RecordPaymentAsync` makes), so the two views of "how much's been
    paid" never drift apart; a `PurchaseReturn`/`OpeningBalance`
    reference is just a label, since neither tracks its own payment state.
  - **Opening Balances** — the one-time starting position for a Party or
    a Chart-of-Accounts account (exactly one of the two, enforced both
    here and by `CK_OpeningBalances_ExactlyOneTarget`); full CRUD, since
    nothing else references an opening balance's own id.

- **Reports** (`Application/Reports/`, `Api/Controllers/
  ReportsController.cs`) — read-only aggregates over data every other
  module already owns; nothing here writes anything. Sales Summary
  (`Paid` orders in a date range, with a payment-mode breakdown), Purchase
  Summary (`Posted` invoices/returns), VAT Summary (sales VAT collected
  minus purchase VAT paid = net payable), Stock Valuation (current
  ledger balance × `PurchasePrice` per product), and Trial Balance
  (`Posted` journal entries plus opening balances, netted per account as
  of a date). Honest about its own limit: Sales and Purchase don't
  auto-post journal entries yet, so a Trial Balance today is only as
  complete as what's been manually recorded against it — not a stand-in
  for a real general ledger until that wiring exists.

- **Utility** (`Application/Utility/`, `Api/Controllers/
  PrintersController.cs` + `PaymentModesController.cs` +
  `AuditLogController.cs` + `BackupsController.cs`) — the smaller
  Settings-screen pieces. Printers: CRUD per branch, `Station` (Kitchen/
  Bar) only meaningful — and only accepted — on a `Kot`-type printer.
  Payment Modes: all five (`Cash`/`Card`/`ESewa`/`Khalti`/`BankTransfer`)
  lazy-seeded the first time anyone asks (Cash/Card on by default), same
  pattern as Payroll Settings — no explicit init step; `PATCH
  .../{code}` just flips one on or off.

  **Audit Log** (`GET /api/utility/audit-log`) is read-only — every write
  comes from `Application/Common/IAuditLogger.cs`, injected into the
  business-significant moments across every other module (never every
  mutation — a document actually posted/completed/cancelled, a deletion,
  an account created/deactivated/password-reset — not a quick-add lookup
  or a routine field edit): `Products`/`Parties`/`Employees` delete,
  `Roles` create/delete, `Users` create/deactivate/reset-password,
  `Orders` close/cancel, `PurchaseInvoices`/`PurchaseReturns` post,
  `PurchaseInvoices` cancel, `StockTransfers` post, `PayrollRuns`
  complete, `JournalEntries` post/cancel, and `PartyPayments` create — 18
  call sites in all. A logging failure is swallowed, never rethrown — see
  `IAuditLogger`'s class remarks for why a missing audit row is far less
  harmful than losing a legitimate transaction over a logging hiccup.

  **Backup** (`GET`/`POST /api/utility/backups`) runs a real T-SQL
  `BACKUP DATABASE` against the install's own SQL Server — not a
  simulated log entry — to a directory configured via `Backup:Directory`
  in `appsettings.json` (or the `Backup__Directory` env var), which must
  already exist and be writable by the *SQL Server service account*, not
  the API process; unconfigured or unwritable both fail with a clear
  message (400) and — for the latter — a `Failed` row in the log with no
  size, distinct from never having run at all. No restore and no
  scheduling in this pass — `BackupAtUtc`'s "NULL = automatic/scheduled"
  remark in `BackupLogEntry` describes a future addition, not something
  built yet.

### `ICurrentUserContext` — resolving "who's asking"

`Application/Common/ICurrentUserContext.cs` is the interface every module
above Setup/Auth depends on to get the caller's `CompanyId`/`UserId`/
`BranchId` without knowing anything about HTTP or JWTs.
`Api/Auth/HttpCurrentUserContext.cs` is the only implementation, reading the
`companyId`/`sub`/`branchId` claims `JwtTokenService` puts on the token at
login. Registered per-request (`AddScoped`) in `Program.cs` alongside
`AddHttpContextAccessor()`. Every controller that needs it just requires
`[Authorize]` — Setup and Auth are the only controllers that don't, since
they run before a token exists.

`Program.cs` also sets `JwtSecurityTokenHandler.DefaultMapInboundClaims =
false` at startup — without it, ASP.NET Core silently renames the standard
`sub` claim to a long `ClaimTypes.NameIdentifier` URI on validation, so
`HttpCurrentUserContext`'s `FindFirst("sub")` would never match what the
token actually carries. This was a real bug, not a preemptive note: it
shipped invisibly through the whole Masters module (which only ever reads
the unaffected custom `companyId` claim) and only surfaced once Sales
needed `UserId` for real — caught live, with a full stack trace, and fixed
before delivery.

### Adding a new module

Follow the same four-file pattern the modules above use:

1. **Domain** — already done for every table (see `Domain/<Module>/`).
2. **Infrastructure** — already done: every entity has a configuration in
   `Persistence/Configurations/<Module>Configurations.cs`.
3. **Application** — add `Application/<Module>/`: a DTO file, an
   `I<Module>Service` interface, and its implementation, injecting
   `MasterPosDbContext` and `ICurrentUserContext` (plus `ITokenService`/
   `IPasswordHasher<User>` only if the module specifically needs them).
   Register the service in `Application/DependencyInjection.cs`.
4. **Api** — add `Api/Controllers/<Module>Controller.cs`: `[Authorize]`,
   thin, catches `AppException` and maps it to the right 4xx (`NotFound`
   for "doesn't exist", `Conflict` for "exists but can't do that right
   now" — see `ProductsController.Delete` for a controller that needs both).

## Configuration

`appsettings.json` ships with placeholder values — every install must
replace both before going live:

- **`ConnectionStrings:Default`** — this client's local SQL Server instance.
  Can also be set via the `MASTERPOS_CONNECTION_STRING` env var, or
  overridden outright via the standard ASP.NET Core
  `ConnectionStrings__Default` env var (double underscore).
- **`Jwt:SigningKey`** — a unique random secret (32+ bytes) generated per
  install. Never reuse the placeholder in `appsettings.json` and never share
  a key across clients — each install is an independent server, so each
  needs its own.

## Deployment model

Each client runs MasterPOS entirely on their own machine: their computer is
the server, running both the API and a local SQL Server instance, with no
dependency on the internet or a shared backend. This keeps after-sale
support simple (SQL Server was chosen specifically for this — see
`../database/00_README.md`) and matches the schema's `CompanyId`-on-every-table
design, which is ready to support multiple companies per database if this
ever moves to a shared SaaS deployment later, without a schema rewrite —
today, every install just has exactly one Company row.

## Running locally

```bash
cd src/MasterPOS.Api
dotnet ef database update --project ../MasterPOS.Infrastructure   # applies the schema
dotnet run
```

Swagger UI is available at `/swagger` in the Development environment, with
a "Bearer" auth box pre-wired — paste a token from `/api/auth/login`
(without the `Bearer ` prefix; Swagger adds it).

## How this was verified

Every layer here was checked against a real SQL Server 2022 instance, not
just read for correctness:

- The EF Core model was migrated (`dotnet ef migrations add`) and applied
  (`dotnet ef database update`) to a live database.
- Table count (42, including `__EFMigrationsHistory`), foreign key count
  (89), and `CHECK` constraint count (43) all match the raw SQL in
  `../database/` exactly, constraint-name-for-constraint-name — including
  `CK_Products_ProductType`, added alongside the `Inventory`/`Service`/
  `Recipe`/`Consumable` field itself.
- An invalid enum value insert (`BusinessType = 'NotARealType'`) was
  confirmed to be rejected at the database level with the correct
  constraint name in the error.
- The Setup → Login flow was exercised live end-to-end: `POST /api/setup`
  followed by `GET /api/setup/status` (flips to complete), a second
  `POST /api/setup` (rejected, 400), `POST /api/auth/login` with the right
  password (200, JWT + full permission matrix), and with the wrong password
  (401) — then the resulting rows in `Core.Companies`, `Core.Branches`,
  `Auth.Roles`, `Auth.RolePermissions`, and `Auth.Users` were checked
  directly in the database.
- The Masters slice was exercised live end-to-end, past login, with a real
  JWT on every request: created a Category, two Units, and a Warehouse via
  the quick-add endpoints; created "Rice" (`Inventory`) and "Veg Thali"
  (`Recipe`); set Veg Thali's BOM to include Rice (200 OK) and confirmed
  `GET .../bom` reads it back; confirmed setting a BOM on a non-Recipe
  product is rejected (400), a Recipe can't use itself as an ingredient
  (400), and a `Service`-type product can't be used as an ingredient (400,
  with the product's name in the message); confirmed a duplicate barcode
  is rejected (400) and an unauthenticated request is rejected (401);
  confirmed deleting a product still referenced by an active recipe is
  rejected (409, not 404) while deleting a genuinely nonexistent product
  correctly returns 404 — that distinction was a real bug caught live
  during this pass (both cases originally returned 404) and fixed before
  delivery, not just imagined and written up.
- The transaction-lock rule was proven with a real transaction, not a
  mocked one: created "Rice", confirmed a full edit succeeded (200) with
  zero transaction history yet, inserted an actual row into
  `Inventory.StockLedgerEntries` referencing it directly via `sqlcmd`
  (standing in for the Purchase/Inventory module, not yet built), then
  confirmed the same edit now fails (409), `DELETE` also fails (409), and
  `PATCH .../active` still succeeds (200) — with the product's name and
  price left exactly as they were at transaction time, only `IsActive`
  changed.
- The Sales slice was run through a complete real order, live: set up
  "Rice"/"Dal" (`Inventory`), "Veg Thali" (`Recipe`, BOM = 0.2 kg Rice +
  0.15 kg Dal), "Delivery Charge" (`Service`, VAT-exempt), and "Thermal
  Paper Roll" (`Consumable`) against a Dine-in table; opened an order on
  that table (table flipped to `Occupied`); confirmed adding the
  Consumable line is rejected (400); added 2× Veg Thali + 1× Delivery
  Charge and confirmed the totals by hand — Rs. 660 subtotal, Rs. 72.80 VAT
  (only the VAT-applicable line), Rs. 733 grand total with Rs. 0.20
  round-off; printed the KOT (one Kitchen ticket, 1 line — Delivery Charge
  correctly excluded, having no station) and confirmed reprinting
  immediately after is rejected (400, nothing new pending); took a Rs. 400
  partial payment (status → `PartiallyPaid`, table → `PartiallyPaid`) then
  the Rs. 333 remainder (status → `Paid`, table → `Vacant`); read
  `Inventory.StockLedgerEntries` directly afterward and confirmed exactly
  two rows — 0.4 kg Rice out, 0.3 kg Dal out, both `ReferenceType = Order`
  — with no row at all for the Recipe product itself or the Service line;
  then confirmed both the closed-order line-add guard (400) and the
  Masters transaction lock (409, this time against a transaction the
  Application layer itself created, not one inserted by hand) fire
  correctly against "Rice" afterward. A real bug was caught and fixed
  during this pass — see `ICurrentUserContext` above.
- The Purchase slice was run through a complete real invoice and a return,
  live: created a Supplier Party, "Rice" (`Inventory`); confirmed adding a
  `Recipe` or `Service` product to an invoice line is rejected (400);
  built a line matching the original Purchase design mockup's own worked
  example — 100 kg @ Rs. 350, 5% discount, 13% VAT — and confirmed the
  line amount comes out to exactly Rs. 37,651.60 by hand before checking
  the API's number, not after; posted the invoice and read
  `Inventory.StockLedgerEntries` directly afterward, confirming one row,
  `QuantityIn = 100`, `ReferenceType = PurchaseInvoice`; confirmed a
  posted invoice rejects further line edits and `PUT` (400, directing to
  Purchase Return rather than un-posting) and that `Cancel` on a Posted
  invoice is rejected while it still works on a fresh Draft; recorded a
  partial payment and confirmed `AmountPaid` updated independent of
  posting; re-confirmed the Masters transaction lock now fires on "Rice"
  (409) against this Application-generated invoice line, the second
  independent module to prove that rule; created a Purchase Return against
  the posted invoice for 5 kg, posted it, and read the ledger again —
  a second row, `QuantityOut = 5`, `ReferenceType = PurchaseReturn`; and
  finally reconciled net stock by hand across both rows (100 in, 5 out =
  95 kg) against a direct `SUM(QuantityIn) - SUM(QuantityOut)` query on the
  same table, confirming they agreed exactly before considering the module
  done.
- The Inventory slice was run through every document type live: set opening
  stock of 50 kg Rice at "Main Store" (200), confirmed a second opening
  stock call for the same product/warehouse is rejected (400, directing to
  Stock Adjustment) and that one against a `Service` product is rejected
  too (400); posted a -2 kg adjustment (breakage) and confirmed a zero
  quantity-change is rejected (400); created a 10 kg Main Store → Kitchen
  Store transfer and confirmed it comes back `Pending` with nothing moved
  yet, that the same-warehouse case is rejected (400) up front, and that a
  wildly oversized transfer (1000 kg) is accepted as `Pending` but rejected
  on `.../post` (400, "only 48 available") rather than being blocked at
  creation; posted the valid 10 kg transfer and confirmed both a
  `TransferOut` row at Main Store and a `TransferIn` row at Kitchen Store;
  confirmed a `Completed` transfer can't be posted again (400) and a still-
  `Pending` one can still be cancelled; read `.../reports/ledger` and
  confirmed the running balance by hand at every step (50 → 48 → 38 for
  Main Store, 0 → 10 for Kitchen Store) and `.../reports/balances`
  independently agreed; pushed one more -30 kg write-off specifically to
  cross the product's `ReorderLevel` of 20 and confirmed
  `.../reports/reorder-suggestions` picked it up the moment total stock
  (18 kg across both warehouses) fell at or below that level, with
  `shortBy` computed correctly (2 kg) — and stayed silent before that
  point; cross-checked the company's entire Rice balance directly against
  `SUM(QuantityIn) - SUM(QuantityOut)` on `StockLedgerEntries` via
  `sqlcmd` and it matched the API's number exactly; and re-confirmed the
  Masters transaction lock fires on "Rice" (409 on both edit and delete,
  200 still on deactivate) against this Inventory-generated history — the
  third independent module to prove that rule, after Sales and Purchase.
  One real environment mistake was caught and corrected mid-pass, not a
  code bug: `dotnet run`'s `ConnectionStrings:Default` in `appsettings.json`
  takes precedence over the `MASTERPOS_CONNECTION_STRING` env var (the code
  only falls back to the env var when the JSON value is empty — see
  `Program.cs`), so the API kept trying `localhost:1433` (the placeholder)
  until the test was switched to the ASP.NET Core `ConnectionStrings__Default`
  double-underscore override instead; `dotnet ef database update` is
  unaffected, since its design-time factory (`DesignTimeDbContextFactory.cs`)
  reads `MASTERPOS_CONNECTION_STRING` directly rather than through
  `appsettings.json`.
- The Workforce slice was run through a full payroll cycle live: created
  two employees — Amit Kadam (Rs. 30,000 basic, 09:00–18:00 shift) and
  Sita Rai (Rs. 25,000 basic, no shift); marked a partial August for both
  (Amit: 5 present + 1 absent + 1 present-with-3h-OT) and confirmed
  `.../check-in` rejects a second same-day call (400) while `.../check-out`
  correctly computes zero overtime for Sita's shift-less record; requested
  and approved a same-day Sick leave for Amit and confirmed `GET
  .../today` showed him as `OnLeave` with no check-in/out at all — read
  from the leave request, not a fabricated Attendance row — while Sita's
  real check-in showed through; gave Amit a Rs. 5,000 advance; then fully
  marked July 2026 for both (31/31 days, Sita clean, Amit with one
  `Absent`) and ran payroll for that period — hand-verified before
  checking the API's numbers: Amit's basic came out to Rs. 29,032.26
  (Rs. 30,000 − one day's pro-rated rate), net Rs. 24,032.26 after the
  full Rs. 5,000 advance deduction, flagged `LeaveDeduction`; Sita's
  Rs. 25,000 flat, flagged `Ready`; gross/net payroll totals matched the
  sum by hand. Confirmed a duplicate run for the same branch/period is
  rejected (400), `.../recompute` reproduces identical numbers on a still-
  Draft run, and `.../complete` actually recovers the advance — read
  `EmployeeAdvances` directly before (Rs. 5,000 outstanding, `Open`) and
  after (Rs. 0 outstanding, `Recovered`) — and then locks the run
  (`.../recompute` and a second `.../complete` both now 400). Ran payroll
  again for the still-open August period and confirmed every line came
  back `AttendancePending` (correctly, since far fewer days were marked
  than had elapsed) with the exact same hand-checked OT math; deactivated
  Amit and confirmed a September run picked up only Sita. Every number
  was cross-checked directly against `Workforce.PayrollRunLines` and
  `Workforce.EmployeeAdvances` via `sqlcmd`, and the Masters-style
  deletion guard (409 with history, `PATCH .../active` still 200) was
  confirmed on Amit — full edits stayed allowed throughout, deliberately
  unlike Products, since a salary change doesn't rewrite any past run's
  already-snapshotted line.
- Payroll Settings / Tax Slabs / statutory deductions were added in a
  second pass and re-verified with a full clean rebuild — new migration
  regenerated from scratch (43 tables, 91 FKs, 52 CHECK constraints, all
  matching the raw SQL exactly, `CK_Employees_MaritalStatus` and
  `CK_PayrollRuns_RunType` included), applied to a fresh database, then
  exercised live: confirmed `GET .../payroll-settings` auto-creates the
  defaults row with no explicit init step; `.../tax-slabs/seed-defaults`
  loads the 10-row Single/Couple structure, refuses to run again once
  populated (400), and rejects a manually-added overlapping range (400,
  naming the slab it collides with); created a Single employee (Rs.
  50,000 basic, 4h OT one day) and a Couple employee (Rs. 40,000 basic,
  no OT), enabled OT/PF(10%/10%)/SSF(11%/20%)/TDS together, fully marked
  July 2026 for both, and ran Monthly payroll — every figure hand-computed
  to the rupee *before* checking the API's response: Amit's OT
  Rs. 1,209.68, PF Rs. 5,000/Rs. 5,000, SSF Rs. 5,500/Rs. 10,000, TDS
  Rs. 407.10 (annualized taxable Rs. 488,516.16, entirely inside the first
  1% Single band), net Rs. 40,302.58; Sita's PF Rs. 4,000/Rs. 4,000, SSF
  Rs. 4,400/Rs. 8,000, TDS Rs. 316.00 (annualized Rs. 379,200, entirely
  inside the first 1% Couple band), net Rs. 31,284.00 — both matched
  exactly, employer-side PF/SSF confirmed present on the line but never
  subtracted from NetPay. Created a `FestivalBonus` run for the same
  period alongside the Monthly one (both coexist — confirmed the
  Branch/Month/Year/RunType uniqueness is per-type, not shared) and got
  exactly `BasicSalary × 100%` per employee with zero OT/PF/SSF/TDS
  applied to it, then confirmed creating a `FestivalBonus` run is rejected
  outright (400) once `FestivalBonusEnabled` is turned back off in
  Settings. Turned every toggle off (OT/PF/SSF/TDS) and re-ran a period
  where an employee had 5 logged OT hours — confirmed OT/PF/SSF/TDS all
  came back exactly zero and NetPay equalled Basic exactly, proving each
  toggle actually gates its own calculation rather than the numbers just
  happening to be small. Completed the July Monthly run and cross-checked
  every PF/SSF/TDS figure directly against `Workforce.PayrollRunLines` via
  `sqlcmd` — exact match, to the rupee, both employees.
- Parties, Dining Tables, and Roles/Users were added in a third pass —
  no schema changes needed, since all five tables were already fully
  configured from earlier CHECK-constraint parity work, just missing
  their Application/Api layers. Verified live: created a Supplier and
  confirmed the Party-level transaction lock fires (409) the moment a
  real Purchase Invoice is opened against it, same as Products; created
  two Dining Tables, opened a real Sales order on one and confirmed its
  status flipped to `Occupied` and both edit and delete were rejected
  while it stayed that way; created a `Cashier` role and confirmed both
  a partial permission matrix (400, naming every missing module) and any
  edit/delete attempt on the seeded system `Admin` role (400/409) are
  rejected; created a Cashier user, logged in as them for real, then
  deactivated them and confirmed login now fails (401) even with the
  right password, confirmed the caller can't deactivate their own
  account (400), and confirmed `.../reset-password` immediately
  invalidates the old password and accepts the new one on login.
- Accounting, Reports, and Utility were added in a fourth pass — again no
  schema changes needed, all nine tables already fully configured.
  Verified live: seeded the default Chart of Accounts and confirmed
  reseeding (400) and deleting a system account (409) are both rejected;
  built a Journal Entry and confirmed posting is blocked with zero lines,
  with only one line, and with mismatched debit/credit totals (400 each,
  the last one naming both totals) before a correctly balanced Rs. 50,000
  entry posted clean; ran a real Purchase Invoice (Rs. 10,000, Posted)
  through to a Party Payment against it — confirmed an overpayment is
  rejected (400) and a correct Rs. 6,000 partial payment advanced the
  invoice's own `AmountPaid` to exactly 6,000, read back from the invoice
  directly, not just the payment record; confirmed Opening Balance
  rejects both Party+Account together and neither (400 each). Ran a real
  Sales order (2kg Rice, Rs. 949 grand total, Rs. 109.20 VAT) to `Paid`
  and cross-checked all four Reports against it and the earlier Purchase
  invoice: Sales Summary matched the order exactly including its Cash
  payment-mode breakdown; Purchase Summary showed the Rs. 10,000 invoice;
  VAT Summary showed Rs. 109.20 net payable (sales VAT only, since the
  test invoice was zero-VAT); Stock Valuation correctly showed 98kg
  Rice × Rs. 350 = Rs. 34,300, reflecting the sale's 2kg deduction from
  the purchase's 100kg; Trial Balance combined the posted Journal Entry
  with a standalone Bank opening balance into Rs. 75,000 debit / Rs.
  50,000 credit — deliberately unbalanced, since the test opening balance
  had no offsetting entry, proving the report reflects real data rather
  than silently forcing a balance. Confirmed a Kot printer accepts a
  Station and a Receipt printer rejects one (400), and Payment Modes
  lazy-seeds all five with Cash/Card on by default.
- Discount Offers, the Audit Log, and real Backups closed out every
  remaining named backend gap, in a fifth pass — again no schema
  changes, and re-confirmed with a full clean rebuild from a fresh
  migration (44 tables, 91 FKs, 52 CHECK constraints, unchanged). Verified
  live: a two-line order (one VAT-applicable Rs. 500 line, one VAT-exempt
  Rs. 300 line) gave Rs. 65.00 VAT and Rs. 865 grand total with no
  discount, matching the pre-existing formula exactly; applying a 10%
  manual discount then gave Rs. 80.00 discount, Rs. 58.50 VAT (hand-
  computed: the discount prorates to Rs. 50 off the Rs. 500 vatable
  portion, leaving Rs. 450 taxable × 13%), Rs. 0.50 round-off, Rs. 779
  grand total — exact match; an expired Discount Offer was rejected
  (400, naming the expiry date) and a flat Rs. 1,000 offer against an
  Rs. 800 bill correctly capped at Rs. 800, zeroing the VAT and the
  grand total entirely rather than going negative; a Percent value over
  100 was rejected at both offer-creation and manual-apply time. Closed
  a real order and confirmed the Audit Log picked it up with the exact
  order number and amount; created a Role and a User and confirmed both
  logged correctly, with `entityType` filtering working and newest-first
  ordering correct. Triggered a real backup and confirmed an actual
  `.bak` file landed on disk (read back directly, not just trusted from
  the API's response) with a size matching `msdb.dbo.backupset` to the
  byte; confirmed an unconfigured backup directory fails before ever
  touching SQL Server (400, clear message) and an unwritable one fails
  with the real SQL Server error surfaced to the caller *and* a `Failed`
  row recorded in the log with no size — proving the failure path writes
  history too, not just the happy path.

`tests/MasterPOS.Tests` is still the project-template scaffold — automated
tests for these slices are a good next step, but everything above was
validated by hand against a live database rather than left unverified.
