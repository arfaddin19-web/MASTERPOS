# MasterPOS Frontend (React + TypeScript)

The web client for MasterPOS, built to talk to the ASP.NET Core backend in
`../backend/` over its real HTTP API — no mocking, no fixtures. Every screen
in the sidebar is live: **Setup, Login, Dashboard, Billing (POS), Masters,
Inventory, Transactions, Reports, Workforce, and Settings** are all built
and wired to the real backend, covering every module the backend exposes.

## Stack

- **React 18** + **TypeScript**, built with **Vite 8**
- **React Router v6** for routing, including the setup/auth redirect gate
- **TanStack Query** for all server state — fetching (`useQuery`) and
  mutations (`useMutation`), with `queryClient.setQueryData`/
  `invalidateQueries` used to keep the UI in sync with what the backend
  actually persisted, not an optimistic guess
- **Axios**, with one shared client (`src/api/client.ts`) that attaches the
  JWT to every request and handles session-expiry centrally

No CSS framework — the entire design system is hand-ported from the design
canvas's `.dc.html` mockups (`src/styles/global.css`), token-for-token: the
same oklch color variables (`--bg`, `--surface`, `--gold`, `--gold-bright`,
`--success`, `--danger`, …), the same `Instrument Serif` (display/headers,
italic) + `Manrope` (body) pairing via Google Fonts, the same dark theme
throughout. Mockup `<div>`-based interactive elements (nav items, tabs,
steppers, category pills, toggle switches) were converted to real
`<button>`/`<a>` elements with `background:none;border:none` resets, so
they're keyboard- and screen-reader-usable, not just visually identical.
A handful of pages needed shapes the mockups didn't fully spell out (a
multi-document Transactions screen, a per-report Reports sidebar, a
per-role Roles & Permissions editor) — those were designed to fit the same
visual language rather than left as placeholders; each is called out below.

## Architecture

```
src/
  api/            typed Axios wrappers, one file per backend module
    client.ts     shared instance: JWT interceptor, 401 → auto-logout, apiErrorMessage()
    types.ts      TS interfaces mirroring the backend's DTOs field-for-field
    auth.ts, auth-admin.ts, setup.ts, masters.ts, sales.ts, inventory.ts,
    purchase.ts, accounting.ts, reports.ts, workforce.ts, utility.ts
  auth/
    AuthContext.tsx   session state (JWT + user + permission matrix), persisted to localStorage
    RequireAuth.tsx   route guard, redirects to /login when no session
  components/
    AppShell.tsx      the sidebar + topbar shell every authenticated page renders inside
    icons.tsx         inline SVG icons, ported from the mockups
    Shared.tsx        Tabs / Switch / Modal / Banner+useBanner — the small pieces every
                       module page below reuses instead of six near-duplicate versions
  lib/format.ts       formatRs / formatDate / formatDateTime / todayIso — shared once
  pages/
    SetupPage, LoginPage, DashboardPage, PosPage        — the original vertical slice
    MastersPage → masters/{Products,Lookup,Parties,Tables,DiscountOffers}Tab.tsx
    InventoryPage → inventory/{Overview,Adjustments,Transfers,OpeningStock,Ledger}Tab.tsx
    TransactionsPage → transactions/{PurchaseInvoice,PurchaseReturn,JournalEntry,
                        PaymentEntry,OpeningBalance,ChartOfAccounts}Tab.tsx
    ReportsPage                                          — category sidebar + report body
    WorkforcePage → workforce/{Employees,Attendance,Leave,Advances,Payroll}Tab.tsx
    SettingsPage → settings/{Roles,Users,Utilities}Tab.tsx
  styles/global.css    the whole design system, ported from the design canvas
  App.tsx              route table + the /-redirect gate (checks setup status first)
  main.tsx             QueryClientProvider → BrowserRouter → AuthProvider → App
```

### Auth pattern

Login returns a JWT plus the caller's full permission matrix in one call
(`POST /api/auth/login`). The token is stored under `localStorage["masterpos.token"]`;
the rest of the response (user info + permissions) under
`localStorage["masterpos.session"]`, so a page reload restores the session
without hitting the backend again. `apiClient`'s response interceptor clears
both and redirects to `/login` on any `401` — except from the login endpoint
itself, so a *wrong password* shows an inline error instead of bouncing the
user off the login page they're already on.

### Dev proxy, not CORS

`vite.config.ts` proxies `/api/*` to the backend (`http://localhost:5080` by
default, overridable via `VITE_BACKEND_URL`), so the browser sees every
request as same-origin and the backend needs zero CORS configuration. This
mirrors the backend's own local-install deployment model — in production the
built frontend (`npm run build` → `dist/`) is meant to be served from the
same origin as the API, not as a separately-hosted SPA.

### The one deliberate design deviation

The POS mockup (`POS.dc.html`) uses its own dedicated 76px icon-only
sidebar, different from the rest of the app's 232px labeled sidebar.
`PosPage` reuses the standard `AppShell` (232px sidebar) instead, so
navigation stays consistent everywhere in the app rather than the layout
jumping between two different chrome styles the moment billing opens.
Everything inside the POS *content area itself* (order-type tabs, product
grid, cart, discount panel, payment methods) is ported 1:1 from the mockup.

### Real backend enum values, not guessed ones

A few backend fields are stored as short C# enums rather than the longer
English words a UI would naturally reach for — `BalanceType { Dr, Cr }` (not
`Debit`/`Credit`), `PartyPaymentDirection { Paid, Received }` (not
`In`/`Out`). These were caught live (see **How this was verified**) by
cross-referencing every dropdown/toggle value actually sent to the API
against `MasterPOS.Domain.Common.Enums.cs`, not assumed from field names.

## What's built

Every module the backend exposes now has a screen:

| Module | Screens |
|---|---|
| Setup / Login / Dashboard | ✅ Setup wizard, login, dashboard KPIs |
| Billing (POS) | ✅ Cart, discounts, KOT, split/full payment, hold/cancel |
| Masters | ✅ Products (incl. Recipe/BOM builder), Categories, Units, Warehouses, Parties, Dining Tables, Discount Offers |
| Inventory | ✅ Stock balances/valuation overview, Adjustments, Transfers, Opening Stock, full Ledger |
| Transactions | ✅ Purchase Invoice, Purchase Return, Journal Entry, Payment Entry, Opening Balance, Chart of Accounts |
| Reports | ✅ Sales Summary, Purchase Summary, VAT Summary, Stock Valuation, Trial Balance |
| Workforce | ✅ Employees, Attendance (check-in + manual mark), Leave, Advances, Payroll (settings, tax slabs, runs) |
| Settings | ✅ Roles & Permissions (per-role matrix editor), Users (incl. password reset), Utilities (Printers, Payment Modes, Backups, Audit Trail) |

Nothing is a placeholder any more — every nav destination is a real,
API-backed screen.

## Running locally

```bash
npm install
npm run dev            # http://localhost:5173, proxying /api to the backend
```

Requires the backend running at `http://localhost:5080` (or set
`VITE_BACKEND_URL` to point elsewhere) with a migrated database — see
`../backend/README.md`.

```bash
npm run build           # tsc -b && vite build → dist/
npm run lint             # oxlint
```

## How this was verified

Every screen was driven through a real, headless-Chromium Playwright
session against the actual running backend and a real SQL Server database —
not just written and assumed correct, the same standard the entire backend
was held to.

**Pass 1 — the original vertical slice (Setup → Login → Dashboard → POS):**
cold-started against an empty database, walked the 2-step Setup wizard,
logged in, and rang up a real sale — 2× Cappuccino + 1× Bottled Water,
hand-verified subtotal/VAT/total, applied a 10% discount and hand-verified
the discount-before-VAT proration (re-proving a backend bug fixed earlier in
this engagement), incremented via the stepper, and charged the order closed
to `Paid`. See the git history / earlier delivery for the full detail.

**Pass 2 — every remaining module, end-to-end, with real business math
checked by hand before trusting the API's own numbers:**

- **Masters** — created Categories/Units/Warehouses via quick-add; created a
  `Recipe` product (**Veg Thali**) and confirmed the backend's real "a
  recipe needs at least one ingredient" rule fires on save with zero BOM
  lines — the product is still created and the form lands you on its Edit
  view with a clear message, not a lost/orphaned record — then added an
  ingredient and re-saved to confirm the success path; created a Supplier
  and a Customer Party and confirmed both list correctly; created a Dining
  Table and a Discount Offer.
- **Inventory** — recorded Opening Stock (100kg Rice), confirmed the
  Overview's balance and bar-vs-reorder-level rendering updated correctly;
  posted a Stock Adjustment (-2kg breakage) and confirmed it landed in both
  the Adjustments list and the full Ledger with the correct running balance;
  exercised the Transfer create/list flow.
- **Transactions** — built a real Purchase Invoice line-by-line (50kg Rice @
  Rs. 350, 2% discount, 13% VAT) and hand-verified the exact figures before
  checking the screen: Rs. 17,500 subtotal, Rs. 350 discount, Rs. 2,229.50
  VAT (13% of the *discounted* Rs. 17,150 taxable base — the same
  discount-before-VAT rule Sales uses), Rs. 0.50 round-off, **Rs. 19,380.00
  grand total** — posted it and confirmed the status flipped to `Posted`;
  built a Purchase Return against it (5kg) and confirmed Rs. 1,977.50 exactly
  (5 × 350 × 1.13); seeded the default Chart of Accounts; built a balanced
  Journal Entry (Debit Cash Rs. 50,000 / Credit Opening Balance Equity
  Rs. 50,000) and confirmed it only posts once debits equal credits; recorded
  a Payment Entry and an Opening Balance.
- **Reports** — confirmed Purchase Summary aggregated the invoice and return
  above into **Invoices Posted 1 / Rs. 19,380.00, Returns Posted 1 /
  −Rs. 1,977.50, Net Purchase Rs. 17,402.50** — exact arithmetic match,
  independently computed by the backend from the same two documents; checked
  VAT Summary, Stock Valuation, and Trial Balance render correctly.
- **Workforce** — created an employee, checked them in for the day,
  submitted and reviewed a Leave request, recorded an Advance; seeded the
  default Tax Slabs (confirmed all 10 rows — 5 Single + 5 Couple bands —
  actually exist server-side, not just the ones visible above an initial
  screenshot's fold); ran a real Monthly payroll and confirmed the KPI
  cards, the run's line detail, and the Payroll Settings toggles all render
  and persist correctly.
- **Settings** — created a custom Role and confirmed the permission-matrix
  editor's checkboxes actually round-trip through `PUT /api/auth/roles`;
  confirmed the seeded system Admin role loads by default with every
  permission ticked and is read-only (can't be edited or deleted, matching
  the backend's own rule); created a User against that role; added a
  Printer, toggled a Payment Mode, and triggered a real Backup Now.
- **Regression check** — re-ran the original Dashboard and POS smoke test
  after all of the above to confirm none of the shared CSS/component changes
  (the `Switch`/`Tabs`/`Banner` components introduced for the new pages)
  broke the already-verified screens; Dashboard's Stock Value and POS's
  product grid both correctly reflected the new Masters/Inventory data.

A handful of real issues were caught and fixed during this pass, not just
imagined and written up:

- **Party opening balance / payment direction enums** — the UI initially
  sent `"Debit"`/`"Credit"` and `"In"`/`"Out"`, guessed from the field
  names; the backend's actual enums are `BalanceType { Dr, Cr }` and
  `PartyPaymentDirection { Paid, Received }`. Caught by cross-referencing
  every dropdown value against `Enums.cs` after a live 400 response, fixed
  across `types.ts`, `masters.ts`, `accounting.ts`, and the three pages that
  used them.
- **Attendance manual-mark status** — the UI offered a `"Half Day"` option
  that isn't a real `AttendanceStatus` value (`Present`/`Late`/`Absent`/
  `OnLeave` only); replaced with `On Leave`, the actual fourth value.
- **New Recipe product with an empty BOM could silently orphan itself** —
  the original save flow created the product *and* set its BOM in one
  mutation; when the backend correctly rejected a zero-ingredient BOM, the
  whole mutation failed before the UI ever selected the newly-created
  product, so a retry would create a second, duplicate product with no way
  back to the first. Fixed so the product save and selection commit
  immediately, and only the BOM step can still fail — with a message telling
  the user exactly what to do next.
- **Settings → Roles landed on a blank, unselected form** — the permission
  matrix showed nothing until a role was clicked, even though a system Admin
  role always exists after Setup. Fixed to auto-select the first role on
  load (once, via a ref-guarded effect, so it never fights the "+ New Role"
  button's own null-selection state).

`e2e-*.mjs` scratch verification scripts were run against a throwaway Docker
SQL Server instance during development — not committed here, not a
permanent test suite. A proper component/e2e test suite (Playwright or
Vitest + Testing Library) is the natural next step, same as the backend's
own `tests/MasterPOS.Tests` being still-scaffold.

## Next steps

- A real automated test suite, replacing the scratch Playwright scripts used
  during development.
- Code-splitting the production bundle (`vite build` currently warns about
  a single ~500KB JS chunk — fine for a local install, worth revisiting for
  a slower connection).
- Receipt/KOT print layouts (the POS screen prints logically — the KOT
  endpoint call — but there's no dedicated print-preview/thermal-format view
  yet).
