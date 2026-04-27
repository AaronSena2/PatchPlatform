<#
.SYNOPSIS
    Installs the PatchPlatform Agent as a Windows Service and enrolls the device.

.DESCRIPTION
    Publishes the .NET Worker application, writes a production appsettings.json,
    creates a Windows Service, runs device enrollment, and starts the service.

    Must be run as Administrator.
    Re-running this script is safe: an existing service will be stopped,
    reconfigured, and restarted.

.PARAMETER ServerUrl
    Base URL of the PatchPlatform Server (e.g. http://patchserver:5000).

.PARAMETER EnrollmentToken
    One-time enrollment token generated from the server admin UI (/tokens).

.PARAMETER InstallPath
    Directory where the agent will be installed.
    Default: C:\ProgramData\PatchPlatform\Agent

.PARAMETER StatePath
    Path to the agent state file (stores deviceId and secret).
    Default: C:\ProgramData\PatchPlatform\Agent\state.json

.PARAMETER HeartbeatIntervalMinutes
    How often the agent checks in with the server.
    Default: 60

.PARAMETER ServiceName
    Windows Service name.
    Default: PatchPlatformAgent

.PARAMETER SourcePath
    Root of the repository (contains PatchPlatform.slnx).
    Default: parent directory of this script (or current directory as fallback).

.PARAMETER SkipPublish
    Skip the dotnet publish step (use pre-built artifacts already in InstallPath).

.EXAMPLE
    .\Install-Agent.ps1 -ServerUrl http://patchserver:5000 -EnrollmentToken "abc123"

.EXAMPLE
    .\Install-Agent.ps1 -ServerUrl http://patchserver:5000 -EnrollmentToken "abc123" -HeartbeatIntervalMinutes 30
#>
#Requires -RunAsAdministrator
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string]$ServerUrl,

    [Parameter(Mandatory)]
    [string]$EnrollmentToken,

    [string]$InstallPath              = 'C:\ProgramData\PatchPlatform\Agent',
    [string]$StatePath                = 'C:\ProgramData\PatchPlatform\Agent\state.json',
    [int]   $HeartbeatIntervalMinutes = 60,
    [string]$ServiceName              = 'PatchPlatformAgent',
    [string]$SourcePath               = '',
    [switch]$SkipPublish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step([string]$msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-OK([string]$msg)   { Write-Host "    OK: $msg" -ForegroundColor Green }
function Write-Warn([string]$msg) { Write-Host "    WARN: $msg" -ForegroundColor Yellow }

# ── Resolve SourcePath robustly ──────────────────────────────────────────────
if ([string]::IsNullOrWhiteSpace($SourcePath)) {
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { $PWD.Path }
    $candidate = (Resolve-Path (Join-Path $scriptDir '..') -ErrorAction SilentlyContinue)?.Path
    if ($candidate -and (Test-Path (Join-Path $candidate 'PatchPlatform.slnx'))) {
        $SourcePath = $candidate
    } else {
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

# Verify server is reachable via /health — informational only, non-blocking.
Write-Step 'Checking server reachability'
$serverBaseUrl = $ServerUrl.TrimEnd('/')
$healthUrl = "$serverBaseUrl/health"
try {
    $response = Invoke-WebRequest -Uri $healthUrl -TimeoutSec 10 -UseBasicParsing -ErrorAction Stop
    Write-OK "Server is reachable at $serverBaseUrl (HTTP $($response.StatusCode))"
} catch {
    Write-Warn "Server did not respond at $healthUrl — enrollment will be attempted after service install."
    Write-Warn "If enrollment fails, ensure the server is running and reachable from this machine."
}

# ── 2. Publish the application ────────────────────────────────────────────────
if (-not $SkipPublish) {
    Write-Step "Publishing agent to $InstallPath"
    $agentProject = Join-Path $SourcePath 'src\Agent\PatchPlatform.Agent.Service\PatchPlatform.Agent.Service.csproj'
    if (-not (Test-Path $agentProject)) {
        throw "Project file not found: $agentProject"
    }
    & dotnet publish $agentProject `
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

# ── 3. Write production appsettings ──────────────────────────────────────────
Write-Step 'Writing production appsettings.json'
$stateDir = Split-Path $StatePath -Parent
New-Item -ItemType Directory -Force -Path $stateDir | Out-Null

$productionSettings = [ordered]@{
    Logging = [ordered]@{
        LogLevel = [ordered]@{
            Default                    = 'Information'
            'Microsoft.Hosting.Lifetime' = 'Information'
        }
    }
    Agent = [ordered]@{
        ServerBaseUrl            = $serverBaseUrl
        StatePath                = $StatePath
        HeartbeatIntervalMinutes = $HeartbeatIntervalMinutes
    }
}
$settingsJson = $productionSettings | ConvertTo-Json -Depth 10
Set-Content -Path (Join-Path $InstallPath 'appsettings.json') -Value $settingsJson -Encoding UTF8
Write-OK "appsettings.json written"

# ── 4. Enroll the device ──────────────────────────────────────────────────────
Write-Step 'Enrolling device with server'
$exePath = Join-Path $InstallPath 'PatchPlatform.Agent.Service.exe'
if (-not (Test-Path $exePath)) {
    throw "Executable not found: $exePath. Ensure the publish step succeeded."
}

# Run enrollment (exits after completing)
& $exePath --enroll --token $EnrollmentToken --server $serverBaseUrl
if ($LASTEXITCODE -ne 0) {
    throw "Enrollment failed (exit $LASTEXITCODE). Verify that the enrollment token is valid, " +
          "has not expired, and that the server at $serverBaseUrl is running."
}
Write-OK "Device enrolled. State saved to $StatePath"

# ── 5. Install the Windows Service ───────────────────────────────────────────
Write-Step "Installing Windows Service '$ServiceName'"
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Warn "Service '$ServiceName' already exists — stopping and reconfiguring."
    if ($existing.Status -ne 'Stopped') {
        Stop-Service -Name $ServiceName -Force
        $existing.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(10))
        Write-OK 'Stopped existing service'
    }
    & sc.exe config $ServiceName binpath= "`"$exePath`"" start= auto | Out-Null
    Write-OK "Service '$ServiceName' reconfigured"
} else {
    New-Service -Name $ServiceName `
                -DisplayName 'PatchPlatform Agent' `
                -Description 'PatchPlatform patch management agent (heartbeat, inventory, dry-run updates)' `
                -BinaryPathName "`"$exePath`"" `
                -StartupType Automatic | Out-Null
    Write-OK "Service '$ServiceName' created"
}

# ── 6. Start the service ──────────────────────────────────────────────────────
Write-Step "Starting service '$ServiceName'"
Start-Service -Name $ServiceName
$svc = Get-Service -Name $ServiceName
Write-OK "Service status: $($svc.Status)"

# ── Done ──────────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '══════════════════════════════════════════════════════════' -ForegroundColor Green
Write-Host '  PatchPlatform Agent installed successfully!' -ForegroundColor Green
Write-Host "  Server : $serverBaseUrl" -ForegroundColor Green
Write-Host "  State  : $StatePath" -ForegroundColor Green
Write-Host "  Logs   : Get-Content -Path '$InstallPath\logs\*.log' -Wait" -ForegroundColor Green
Write-Host "           (or check Windows Event Viewer > Application log)" -ForegroundColor Green
Write-Host '══════════════════════════════════════════════════════════' -ForegroundColor Green
Write-Host ''
Write-Host "The agent will check in with the server every $HeartbeatIntervalMinutes minutes." -ForegroundColor Yellow
Write-Host "View this device on the server: $serverBaseUrl/devices" -ForegroundColor Yellow
