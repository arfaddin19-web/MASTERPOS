; MasterPOS-Setup.exe — Inno Setup script.
;
; Compiles installer\stage\ (produced by installer\publish.ps1) into a
; single offline installer for a client's Windows PC. Requires Inno Setup 6
; (free — jrsoftware.org/isinfo.php) to compile; open this file in the
; Inno Setup Compiler and click Build (Ctrl+F9). See installer\README.md
; for the full picture — what this does and doesn't automate, and how to
; test it on your own PC first.
;
; NOTE: this script was written and reviewed carefully but could not be
; compiled or run in the environment that produced it (Inno Setup and
; Windows Services are Windows-only) — validate it end-to-end on a real
; Windows PC before relying on it for a client install. installer\README.md
; calls out exactly what to check.

#define MyAppName "MasterPOS"
#define MyAppVersion "1.0.0"
#define MyAppURL "http://localhost:5080/"
#define MyServiceName "MasterPOS"

[Setup]
AppId={{6C9F5C2E-6E4B-4C6A-9E9E-3C6B7E6B9C6A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=MasterPOS-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\MasterPOS.Api.exe
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "stage\api\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "stage\migrator\*"; DestDir: "{app}\migrator"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; A shortcut straight to the running app in the default browser — there's
; no desktop client to launch, MasterPOS.Api.exe runs invisibly as a
; Windows Service.
Name: "{group}\{#MyAppName}"; Filename: "{#MyAppURL}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{#MyAppURL}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Code]
var
  DbPage: TInputQueryWizardPage;
  SigningKey: String;

procedure InitializeWizard;
begin
  DbPage := CreateInputQueryPage(wpSelectDir,
    'Database Connection',
    'Where is this machine''s SQL Server?',
    'MasterPOS needs a SQL Server instance on this machine (or reachable over ' +
    'your local network). If you have not installed one yet, install SQL ' +
    'Server Express first (free — search "SQL Server Express download" on ' +
    'microsoft.com) using its default instance name, then come back and ' +
    'continue this install. The value below matches a default SQL Server ' +
    'Express install; change it if yours differs (a different instance ' +
    'name, or SQL logins instead of Windows auth).');
  DbPage.Add('SQL Server connection string:', False);
  DbPage.Values[0] := 'Server=localhost\SQLEXPRESS;Database=MasterPOS;Trusted_Connection=True;TrustServerCertificate=True;';
end;

function GetConnString(Param: String): String;
begin
  Result := DbPage.Values[0];
end;

// A fresh, random per-install JWT signing key — never the placeholder that
// ships in the committed appsettings.json, and never shared across
// clients. Deliberately the plainest version of this: Random() is a real
// Inno Setup Pascal Script support function (Randomize and GetTickCount
// are not — both were tried here and both failed to compile, since
// Randomize doesn't exist in Pascal Script and GetTickCount is a raw Win32
// API call that needs an `external` DLL import, not a plain identifier).
// Setup's own engine seeds Random on its own, which is why installer
// scripts everywhere use exactly this pattern without ever calling
// Randomize themselves.
function GenerateSigningKey(): String;
var
  Chars: String;
  I: Integer;
  S: String;
begin
  Chars := 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';
  S := '';
  for I := 1 to 64 do
    S := S + Chars[Random(Length(Chars)) + 1];
  Result := S;
end;

function JsonEscape(S: String): String;
begin
  StringChangeEx(S, '\', '\\', True);
  StringChangeEx(S, '"', '\"', True);
  Result := S;
end;

// Writes the per-client config Program.cs loads as the highest-priority
// settings source (see Program.cs's AddJsonFile("appsettings.Local.json"))
// — the connection string just collected, a freshly generated JWT signing
// key, a local backups folder, and "urls" so Kestrel listens on the LAN,
// not just localhost, letting other terminals on the same shop network
// reach this machine.
procedure WriteLocalConfig();
var
  BackupDir: String;
  Json: String;
begin
  BackupDir := ExpandConstant('{app}\Backups');
  ForceDirectories(BackupDir);
  StringChangeEx(BackupDir, '\', '\\', True);

  Json :=
    '{' + #13#10 +
    '  "ConnectionStrings": { "Default": "' + JsonEscape(DbPage.Values[0]) + '" },' + #13#10 +
    '  "Jwt": { "SigningKey": "' + SigningKey + '" },' + #13#10 +
    '  "Backup": { "Directory": "' + BackupDir + '" },' + #13#10 +
    '  "urls": "http://0.0.0.0:5080"' + #13#10 +
    '}' + #13#10;

  SaveStringToFile(ExpandConstant('{app}\appsettings.Local.json'), Json, False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    SigningKey := GenerateSigningKey();
    ForceDirectories(ExpandConstant('{app}'));
    WriteLocalConfig();
  end;
end;

[Run]
; Reinstall/upgrade over an existing install: stop and remove the old
; service registration first (harmless if it doesn't exist yet — sc.exe's
; exit code here isn't treated as fatal by Inno).
Filename: "{sys}\sc.exe"; Parameters: "stop {#MyServiceName}"; Flags: runhidden; StatusMsg: "Stopping any existing MasterPOS service..."
Filename: "{sys}\sc.exe"; Parameters: "delete {#MyServiceName}"; Flags: runhidden

; Apply the database schema — same job `dotnet ef database update` does in
; a dev install, but as a plain .exe so the client machine needs no .NET
; SDK or dotnet-ef tool.
Filename: "{app}\migrator\MasterPOS.Migrator.exe"; Parameters: """{code:GetConnString}"""; Flags: runhidden waituntilterminated; StatusMsg: "Setting up the database (this can take a minute)..."

; Register and start the Windows Service — MasterPOS.Api.exe now runs
; invisibly in the background and restarts automatically on reboot.
Filename: "{sys}\sc.exe"; Parameters: "create {#MyServiceName} binPath= ""{app}\MasterPOS.Api.exe"" start= auto DisplayName= ""MasterPOS"""; Flags: runhidden; StatusMsg: "Registering the MasterPOS service..."
Filename: "{sys}\sc.exe"; Parameters: "description {#MyServiceName} ""POS + ERP + Payroll backend for this machine."""; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "failure {#MyServiceName} reset= 86400 actions= restart/5000/restart/5000/restart/5000"; Flags: runhidden

; Let other terminals on the same shop network reach this machine —
; matches "urls": "http://0.0.0.0:5080" written above. Safe to run again on
; every reinstall; Windows Firewall de-duplicates by rule name.
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""MasterPOS"" dir=in action=allow protocol=TCP localport=5080"; Flags: runhidden; StatusMsg: "Allowing MasterPOS through Windows Firewall..."

Filename: "{sys}\sc.exe"; Parameters: "start {#MyServiceName}"; Flags: runhidden; StatusMsg: "Starting MasterPOS..."

; `sc start` returns once Windows reports the service as running, but the
; very first browser request right after that can still beat it to actually
; accepting connections (confirmed live: the service, port, and static
; files were all completely healthy moments later — this is purely a
; startup race, not a real bug). `ping -n 3 127.0.0.1` is the standard
; installer trick for a ~2 second pause without needing PowerShell.
Filename: "{sys}\ping.exe"; Parameters: "-n 3 127.0.0.1"; Flags: runhidden waituntilterminated

; Offered on the Finish page, unchecked by default in silent installs.
Filename: "{#MyAppURL}"; Description: "Launch MasterPOS in your browser"; Flags: postinstall shellexec skipifsilent

[UninstallRun]
; RunOnceId marks each of these done after the first successful
; uninstall run, so retrying a failed/interrupted uninstall doesn't
; re-stop/re-delete an already-gone service or firewall rule.
Filename: "{sys}\sc.exe"; Parameters: "stop {#MyServiceName}"; Flags: runhidden; RunOnceId: "StopService"
Filename: "{sys}\sc.exe"; Parameters: "delete {#MyServiceName}"; Flags: runhidden; RunOnceId: "DeleteService"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""MasterPOS"""; Flags: runhidden; RunOnceId: "DeleteFirewallRule"

[UninstallDelete]
; appsettings.Local.json and Backups aren't in [Files] (they're generated,
; not shipped) so Inno won't remove them on its own.
Type: files; Name: "{app}\appsettings.Local.json"
Type: filesandordirs; Name: "{app}\Backups"
