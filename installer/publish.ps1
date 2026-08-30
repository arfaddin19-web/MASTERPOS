<#
.SYNOPSIS
  Builds a self-contained MasterPOS release, staged and ready for
  MasterPOS.iss to compile into MasterPOS-Setup.exe.

.DESCRIPTION
  Run this on a Windows machine with the .NET 8 SDK and Node.js 18+
  installed — the machine you build releases on, NOT the client's PC (the
  client's PC needs neither .NET nor Node; the whole point of a
  self-contained publish is that the published .exe carries the .NET
  runtime it needs with it).

  It:
    1. Builds the frontend (`npm run build`) and copies the result into
       MasterPOS.Api's wwwroot, so the published API serves the UI itself
       on the same port — one process, one port, nothing else to run.
    2. Publishes MasterPOS.Api and MasterPOS.Migrator as self-contained
       win-x64 executables under installer\stage\.

  After this finishes, open installer\MasterPOS.iss in Inno Setup Compiler
  (free — jrsoftware.org/isinfo.php) and build it. That produces
  installer\Output\MasterPOS-Setup.exe — the one file you actually hand to
  a client (or run on your own PC to test first). See installer\README.md
  for the full picture, including what the installer does and doesn't
  automate around SQL Server.

.PARAMETER Configuration
  Release (default) or Debug.
#>
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$frontend = Join-Path $repoRoot "frontend"
$backend = Join-Path $repoRoot "backend"
$apiProject = Join-Path $backend "src\MasterPOS.Api\MasterPOS.Api.csproj"
$migratorProject = Join-Path $backend "src\MasterPOS.Migrator\MasterPOS.Migrator.csproj"
$wwwroot = Join-Path $backend "src\MasterPOS.Api\wwwroot"
$stage = Join-Path $PSScriptRoot "stage"

Write-Host "==> 1/4 Building the frontend..." -ForegroundColor Cyan
Push-Location $frontend
npm install
npm run build
Pop-Location

Write-Host "==> 2/4 Copying the frontend build into wwwroot..." -ForegroundColor Cyan
if (Test-Path $wwwroot) { Remove-Item $wwwroot -Recurse -Force }
New-Item -ItemType Directory -Path $wwwroot | Out-Null
Copy-Item (Join-Path $frontend "dist\*") $wwwroot -Recurse

Write-Host "==> 3/4 Publishing MasterPOS.Api (self-contained win-x64)..." -ForegroundColor Cyan
$apiOut = Join-Path $stage "api"
if (Test-Path $apiOut) { Remove-Item $apiOut -Recurse -Force }
dotnet publish $apiProject -c $Configuration -r win-x64 --self-contained true -o $apiOut
if ($LASTEXITCODE -ne 0) { throw "Publishing MasterPOS.Api failed." }

Write-Host "==> 4/4 Publishing MasterPOS.Migrator (self-contained win-x64)..." -ForegroundColor Cyan
$migratorOut = Join-Path $stage "migrator"
if (Test-Path $migratorOut) { Remove-Item $migratorOut -Recurse -Force }
dotnet publish $migratorProject -c $Configuration -r win-x64 --self-contained true -o $migratorOut
if ($LASTEXITCODE -ne 0) { throw "Publishing MasterPOS.Migrator failed." }

Write-Host ""
Write-Host "Done. Staged at $stage" -ForegroundColor Green
Write-Host "Next: open installer\MasterPOS.iss in Inno Setup Compiler and build it." -ForegroundColor Green
