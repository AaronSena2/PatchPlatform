<#
.SYNOPSIS
    Installs the PatchPlatform Server as a Windows Service.

.DESCRIPTION
    Publishes the ASP.NET Core / Blazor Server application, writes a production
    appsettings.json, creates a Windows Service, opens the firewall port, and
    starts the service.

    Must be run as Administrator.
    Re-running this script is safe: an existing service will be stopped,
    reconfigured, and restarted.

.PARAMETER InstallPath
    Directory where the server will be installed.
    Default: C:\ProgramData\PatchPlatform\Server

.PARAMETER PackageStorePath
    Directory where patch packages are stored and served from /content/...
    Default: C:\ProgramData\PatchPlatform\Packages

.PARAMETER SqlConnectionString
    SQL Server connection string.
    Default: SQL Server Express on the local machine.

.PARAMETER ListenPort
    Port the server will listen on.
    Default: 5000

.PARAMETER SigningSecret
    HMAC-SHA256 secret used to sign catalog envelopes.
    If not supplied, a random 64-character secret is generated.

.PARAMETER ServiceName
    Windows Service name.
    Default: PatchPlatformServer

.PARAMETER SourcePath
    Root of the repository (contains PatchPlatform.slnx).
    Default: parent directory of this script (or current directory as fallback).

.PARAMETER SkipPublish
    Skip the dotnet publish step (use pre-built artifacts already in InstallPath).

.EXAMPLE
    .\Install-Server.ps1 -SqlConnectionString "Server=.\SQLEXPRESS;Database=PatchPlatform;Trusted_Connection=True;TrustServerCertificate=True;"

.EXAMPLE
    .\Install-Server.ps1 -ListenPort 8080 -SigningSecret "my-secret-value"
#>
#Requires -RunAsAdministrator
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$InstallPath        = 'C:\ProgramData\PatchPlatform\Server',
    [string]$PackageStorePath   = 'C:\ProgramData\PatchPlatform\Packages',
    [string]$SqlConnectionString = 'Server=.\SQLEXPRESS;Database=PatchPlatform;Trusted_Connection=True;TrustServerCertificate=True;',
    [int]   $ListenPort         = 5000,
    [string]$SigningSecret       = '',
    [string]$ServiceName         = 'PatchPlatformServer',
    [string]$SourcePath          = '',
    [switch]$SkipPublish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step([string]$msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-OK([string]$msg)   { Write-Host "    OK: $msg" -ForegroundColor Green }
function Write-Warn([string]$msg) { Write-Host "    WARN: $msg" -ForegroundColor Yellow }

# ── Resolve SourcePath robustly ──────────────────────────────────────────────
if ([string]::IsNullOrWhiteSpace($SourcePath)) {
    # Prefer the parent of the script file; fall back to the current directory.
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { $PWD.Path }
    $candidate = (Resolve-Path (Join-Path $scriptDir '..') -ErrorAction SilentlyContinue)?.Path
    if ($candidate -and (Test-Path (Join-Path $candidate 'PatchPlatform.slnx'))) {
        $SourcePath = $candidate
    } else {
        # Check if we are already in the repo root
        $candidate2 = (Resolve-Path $scriptDir -ErrorAction SilentlyContinue)?.Path
        if ($candidate2 -and (Test-Path (Join-Path $candidate2 'PatchPlatform.slnx'))) {
            $SourcePath = $candidate2
        } else {
            $SourcePath = $PWD.Path
        }
    }
}

# ── 1. Prerequisites ─────────────────────────────────────────────────────────
Write-Step 'Checking prerequisites'

if (-not $SkipPublish) {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        throw '.NET SDK not found. Install .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0 and ensure it is on PATH.'
    }
    $sdkVersion = & dotnet --version
    Write-OK ".NET SDK $sdkVersion found"

    $slnFile = Join-Path $SourcePath 'PatchPlatform.slnx'
    if (-not (Test-Path $slnFile)) {
        throw "Repository root not found at '$SourcePath' (PatchPlatform.slnx missing). " +
              "Pass -SourcePath pointing to the repo root, or run this script from the repo root."
    }
    Write-OK "Repository root: $SourcePath"
}

# ── 2. Generate signing secret if not supplied ────────────────────────────────
if ([string]::IsNullOrWhiteSpace($SigningSecret)) {
    $rng = [System.Security.Cryptography.RNGCryptoServiceProvider]::new()
    $bytes = New-Object byte[] 48
    $rng.GetBytes($bytes)
    $SigningSecret = [Convert]::ToBase64String($bytes)
    Write-Warn "No -SigningSecret supplied. Generated: $SigningSecret"
    Write-Warn "Record this value — you will need it if you reinstall."
}

# ── 3. Publish the application ────────────────────────────────────────────────
if (-not $SkipPublish) {
    Write-Step "Publishing server to $InstallPath"
    $webProject = Join-Path $SourcePath 'src\Server\PatchPlatform.Server.Web\PatchPlatform.Server.Web.csproj'
    if (-not (Test-Path $webProject)) {
        throw "Project file not found: $webProject"
    }
    & dotnet publish $webProject `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output $InstallPath `
        -p:PublishSingleFile=false
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }
    Write-OK "Published to $InstallPath"
} else {
    Write-Step 'Skipping publish (-SkipPublish)'
    if (-not (Test-Path $InstallPath)) {
        throw "InstallPath '$InstallPath' does not exist and -SkipPublish was set."
    }
}

# ── 4. Write production appsettings ──────────────────────────────────────────
Write-Step 'Writing production appsettings.json'
New-Item -ItemType Directory -Force -Path $PackageStorePath | Out-Null

$productionSettings = [ordered]@{
    Logging              = [ordered]@{
        LogLevel = [ordered]@{
            Default             = 'Information'
            'Microsoft.AspNetCore' = 'Warning'
        }
    }
    AllowedHosts         = '*'
    Urls                 = "http://0.0.0.0:$ListenPort"
    ConnectionStrings    = [ordered]@{
        DefaultConnection = $SqlConnectionString
    }
    PatchPlatform        = [ordered]@{
        PackageStorePath   = $PackageStorePath
        CatalogSigningSecret = $SigningSecret
    }
}
$settingsJson = $productionSettings | ConvertTo-Json -Depth 10
Set-Content -Path (Join-Path $InstallPath 'appsettings.json') -Value $settingsJson -Encoding UTF8
Write-OK "appsettings.json written"

# ── 5. Install the Windows Service ───────────────────────────────────────────
Write-Step "Installing Windows Service '$ServiceName'"
$exePath = Join-Path $InstallPath 'PatchPlatform.Server.Web.exe'
if (-not (Test-Path $exePath)) {
    throw "Executable not found: $exePath. Ensure the publish step succeeded."
}

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Warn "Service '$ServiceName' already exists — stopping and reconfiguring."
    if ($existing.Status -ne 'Stopped') {
        Stop-Service -Name $ServiceName -Force
        # Wait up to 10 s for the service to stop
        $existing.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(10))
        Write-OK 'Stopped existing service'
    }
    & sc.exe config $ServiceName binpath= "`"$exePath`"" start= auto | Out-Null
    Write-OK "Service '$ServiceName' reconfigured"
} else {
    New-Service -Name $ServiceName `
                -DisplayName 'PatchPlatform Server' `
                -Description 'PatchPlatform patch management server (ASP.NET Core + Blazor)' `
                -BinaryPathName "`"$exePath`"" `
                -StartupType Automatic | Out-Null
    Write-OK "Service '$ServiceName' created"
}

# ── 6. Open firewall port ─────────────────────────────────────────────────────
Write-Step "Opening firewall port $ListenPort/TCP"
$ruleName = "PatchPlatform Server (port $ListenPort)"
$rule = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if (-not $rule) {
    New-NetFirewallRule `
        -DisplayName $ruleName `
        -Direction Inbound `
        -Protocol TCP `
        -LocalPort $ListenPort `
        -Action Allow | Out-Null
    Write-OK "Firewall rule created"
} else {
    Write-OK 'Firewall rule already exists'
}

# ── 7. Start the service ──────────────────────────────────────────────────────
Write-Step "Starting service '$ServiceName'"
Start-Service -Name $ServiceName
$svc = Get-Service -Name $ServiceName
Write-OK "Service status: $($svc.Status)"

# ── Done ──────────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '══════════════════════════════════════════════════════════' -ForegroundColor Green
Write-Host '  PatchPlatform Server installed successfully!' -ForegroundColor Green
Write-Host "  URL  : http://localhost:$ListenPort" -ForegroundColor Green
Write-Host "  Logs : Get-Content -Path '$InstallPath\logs\*.log' -Wait" -ForegroundColor Green
Write-Host "         (or check Windows Event Viewer > Application log for '$ServiceName')" -ForegroundColor Green
Write-Host '══════════════════════════════════════════════════════════' -ForegroundColor Green
Write-Host ''
Write-Host 'Next step: open the Admin UI and generate an enrollment token.' -ForegroundColor Yellow
Write-Host "  http://localhost:$ListenPort/tokens" -ForegroundColor Yellow
