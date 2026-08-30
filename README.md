# MasterPOS

A unified POS + ERP + Payroll system for Nepal-market cafes and trading
businesses, deployed once per client on their own machine against their own
local SQL Server. Built and verified end-to-end: schema → backend API →
frontend UI, with every layer checked against a real, running SQL Server
instance and a real browser session — not just written and assumed correct.

## What's in this package

```
database/    Raw T-SQL schema (01_Core_Auth.sql … 08_Utilities.sql) — the
             authoritative source of truth for every table, column, and
             CHECK constraint. See database/00_README.md.
backend/     ASP.NET Core 8 Web API (C#) implementing that schema via EF
             Core — every module (Auth, Setup, Masters, Sales, Purchase,
             Inventory, Workforce/Payroll, Accounting, Reports, Utility) —
             plus an automated xUnit test suite (56 tests, all against a
             real SQL Server). See backend/README.md for architecture and
             how it was verified.
frontend/    React + TypeScript web client covering every module the
             backend exposes — Setup, Login, Dashboard, Billing (POS),
             Masters, Inventory, Transactions, Reports, Workforce, Settings
             — including a light-mode toggle in Settings → Utilities. See
             frontend/README.md for architecture and how it was verified.
installer/   Builds MasterPOS-Setup.exe — an offline Windows installer for
             a client's PC: one self-contained process serving both the
             API and the built frontend, registered as a Windows Service.
             See installer/README.md for how to build and test it.
```

## Quick start (local development)

```bash
# 1. Bring up SQL Server yourself (or point Backend at an existing instance)

# 2. Backend — applies the schema and starts the API on :5080
cd backend/src/MasterPOS.Api
dotnet ef database update --project ../MasterPOS.Infrastructure
dotnet run

# 3. Frontend — proxies /api to the backend, serves the UI on :5173
cd frontend
npm install
npm run dev
```

Open `http://localhost:5173` — it redirects to the First-Time Setup wizard
on an empty database, or straight to Login once a company exists.

## Deployment model

Each client runs MasterPOS entirely on their own machine: their computer is
the server, running both the API and a local SQL Server instance, with no
dependency on the internet or a shared backend. `installer/` packages
exactly that — see its own README for how to build `MasterPOS-Setup.exe`
and how to test it on your own PC first, before it ever reaches a client.
See `backend/README.md`'s own **Deployment model** section for the
reasoning behind this shape (and why SQL Server was chosen in the first
place, in `database/00_README.md`).

## Status

Every module the schema defines has a working backend endpoint, and every
one of those has a working frontend screen — nothing left as a placeholder.
Both layers were validated live: the backend against a real SQL Server 2022
instance (migrations, constraints, and full business transactions run and
hand-verified, plus an automated 56-test xUnit suite covering the same
ground), and the frontend against the real running backend in an actual
browser (Playwright), with money math hand-checked before trusting the
screen's own numbers. See each sub-project's README for the detailed
verification record.
