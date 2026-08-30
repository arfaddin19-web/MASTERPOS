# Building and using the MasterPOS offline installer

This turns the source in `../backend` and `../frontend` into one file —
`MasterPOS-Setup.exe` — that installs and runs MasterPOS on a Windows PC
with nothing else pre-installed except SQL Server. No internet connection
is needed to run MasterPOS day-to-day; the client's PC is the whole server.

**Honesty check first:** the pieces that run on Linux — the backend, the
Migrator, self-contained `win-x64` publishing — were built and verified
against a real SQL Server in this repo's own dev environment (see
`../backend/README.md`). The Inno Setup script (`MasterPOS.iss`) and the
Windows Service registration it drives could **not** be compiled or run in
that same environment — Inno Setup and Windows Services only exist on
Windows. Read this file end to end and do the **"test on your own PC"**
section below before you ever hand `MasterPOS-Setup.exe` to a client.

## What you need to build a release (your machine, not the client's)

- **.NET 8 SDK** — https://dotnet.microsoft.com/download/dotnet/8.0
- **Node.js 18+** — https://nodejs.org
- **Inno Setup 6** — https://jrsoftware.org/isinfo.php (free)

## Building `MasterPOS-Setup.exe`

```powershell
cd installer
.\publish.ps1
```

This builds the frontend, copies it into the API's `wwwroot`, and publishes
`MasterPOS.Api` and `MasterPOS.Migrator` as self-contained `win-x64`
executables under `installer\stage\`. Then:

1. Open `installer\MasterPOS.iss` in the Inno Setup Compiler.
2. Build it (Ctrl+F9, or Build → Compile).
3. `installer\Output\MasterPOS-Setup.exe` is the file you hand to a client
   — or run yourself, to test, first.

Re-run both steps whenever you ship a change.

## What `MasterPOS-Setup.exe` does

1. Asks for the SQL Server connection string (pre-filled with a default
   SQL Server Express instance's — edit it if the target machine's SQL
   Server is set up differently).
2. Copies the published files to `Program Files\MasterPOS\`.
3. Writes `appsettings.Local.json` next to them, with that connection
   string, a freshly generated random JWT signing key (never the
   placeholder committed in source), a `Backups` folder, and `"urls":
   "http://0.0.0.0:5080"` so other till/terminal PCs on the same shop
   network can reach it, not just this machine.
4. Runs `MasterPOS.Migrator.exe` once to create/update the database schema
   — same job `dotnet ef database update` does in a dev install.
5. Registers `MasterPOS.Api.exe` as a Windows Service (`sc.exe create`,
   auto-start, auto-restart on failure) and starts it — no terminal window,
   survives reboots.
6. Opens a Windows Firewall rule for port 5080 and adds Start Menu /
   optional desktop shortcuts pointing at `http://localhost:5080/`.

## What it deliberately does **not** automate: installing SQL Server itself

The wizard's database page tells the operator to install SQL Server first
if it isn't already there, but it doesn't silently download and install it
— that's a ~250MB Microsoft download this repo doesn't bundle or fetch on
your behalf. Before running the installer on a client's PC:

1. Install **SQL Server Express** (free) or Developer edition on that PC —
   search "SQL Server Express download" on microsoft.com. Accept the
   defaults (instance name `SQLEXPRESS`) unless you have a reason not to;
   the wizard's suggested connection string matches that default.
2. *Then* run `MasterPOS-Setup.exe`.

If step 4 above (the Migrator run) fails, the most common cause is this
step being skipped or the connection string not matching what you actually
installed — re-run the installer with the corrected string; it's safe to
run again (it stops/removes any previous service registration first).

## Testing on your own PC first

Exactly what you asked for — do this before any client sees it:

1. Install SQL Server Express on your own PC (if you don't already have a
   SQL Server instance you're fine testing against).
2. Build `MasterPOS-Setup.exe` per the steps above.
3. Run it. Answer the connection-string prompt to match what you installed.
4. When it finishes, open `http://localhost:5080/` (or click the shortcut)
   — you should land on the same First-Time Setup wizard the dev version
   shows on an empty database.
5. Open Services (`services.msc`) — you should see a **MasterPOS** service,
   running, set to Automatic. Stop your PC's network/Wi-Fi entirely and
   confirm the app still works — that's the "offline" claim actually
   proven, not just asserted.
6. Reboot the PC and confirm MasterPOS comes back up on its own with no
   one logging in and starting anything.
7. From a second device on the same network, browse to
   `http://<this-pc's-LAN-IP>:5080/` and confirm it loads too — that's the
   "other terminals in the shop can reach it" part.
8. Uninstall via *Settings → Apps* (or Control Panel) and confirm the
   service is gone from `services.msc` and the `Program Files\MasterPOS`
   folder is removed.

If any of those steps don't behave as described, that's this script
needing a fix, not a mistake on your part — Inno Setup scripts are
notoriously easy to get subtly wrong without a Windows machine to iterate
against, which is exactly the situation this one was written under.

## Everyday operations

- **Logs**: Windows Event Viewer → Windows Logs → Application, source
  ".NET Runtime" / the service's own console output isn't visible since it
  runs headless — for deeper debugging, temporarily run
  `Program Files\MasterPOS\MasterPOS.Api.exe` directly from a terminal
  (stop the service first: `sc stop MasterPOS`) to see console output live.
- **Change the SQL Server connection, JWT key, or backup folder later**:
  edit `Program Files\MasterPOS\appsettings.Local.json` directly, then
  `sc stop MasterPOS && sc start MasterPOS` (as Administrator).
- **Re-run the database migration** (e.g. after updating to a newer
  release with schema changes): `Program Files\MasterPOS\migrator\
  MasterPOS.Migrator.exe "<the same connection string>"`.
- **Back up the database**: Settings → Utilities → Database Backup in the
  app itself writes to the `Backups` folder configured above; that's a
  `.bak` file, restorable with standard SQL Server tooling.
- **Uninstall**: Settings → Apps (or Control Panel → Programs), same as
  any other Windows program — stops and removes the service, deletes the
  install folder including the generated `appsettings.Local.json` and
  `Backups` folder. Back up `Backups` first if you want to keep it.

## Upgrading to a newer release

Re-run a newly built `MasterPOS-Setup.exe` on the same machine. It
reinstalls over the existing files, re-runs the Migrator (safe — it only
applies migrations that haven't run yet), and re-registers the service. No
data is lost; the database itself is untouched except for whatever schema
changes the new release's migrations add.
