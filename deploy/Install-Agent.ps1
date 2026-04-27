<#
.SYNOPSIS
    Installs the PatchPlatform Agent as a Windows Service and enrolls the device.

.DESCRIPTION
    Publishes the .NET Worker application, writes a production appsettings.json,
    creates a Windows Service, runs device enrollment, and starts the service.

    Must be run as Administrator.

.PARAMETER ServerUrl
    Base URL of the PatchPlatform Server (e.g. http://patchserver:5000).

.PARAMETER EnrollmentToken
    One-time enrollment token generated from the server admin UI (/tokens).

.PARAMETER InstallPath
    Directory where the agent will be installed.
    Default: C:\PatchPlatform\Agent

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
    Default: parent directory of this script.

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

    [string]$InstallPath              = 'C:\PatchPlatform\Agent',
    [string]$StatePath                = 'C:\ProgramData\PatchPlatform\Agent\state.json',
    [int]   $HeartbeatIntervalMinutes = 60,
    [string]$ServiceName              = 'PatchPlatformAgent',
    [string]$SourcePath               = $(if ($PSScriptRoot) { (Resolve-Path "$PSScriptRoot\..") } else { $PWD.Path }),
    [switch]$SkipPublish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step([string]$msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-OK([string]$msg)   { Write-Host "    OK: $msg" -ForegroundColor Green }
function Write-Warn([string]$msg) { Write-Host "    WARN: $msg" -ForegroundColor Yellow }

# ── 1. Prerequisites ─────────────────────────────────────────────────────────
Write-Step 'Checking prerequisites'

if (-not $SkipPublish) {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        throw '.NET SDK not found. Install .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0'
    }
    $sdkVersion = & dotnet --version
    Write-OK ".NET SDK $sdkVersion found"
}

# Verify server is reachable (optional, non-fatal)
try {
    $null = Invoke-WebRequest -Uri "$($ServerUrl.TrimEnd('/'))/health" -TimeoutSec 5 -ErrorAction Stop 2>$null
    Write-OK "Server reachable at $ServerUrl"
} catch {
    Write-Warn "Could not reach server at $ServerUrl — will attempt enrollment after service start."
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
    Write-Step 'Skipping publish (--SkipPublish)'
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
        ServerBaseUrl            = $ServerUrl.TrimEnd('/')
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
    throw "Executable not found: $exePath"
}

# Run enrollment (exits after completing)
& $exePath --enroll --token $EnrollmentToken --server $ServerUrl
if ($LASTEXITCODE -ne 0) {
    throw "Enrollment failed (exit $LASTEXITCODE). Check that the token is valid and the server is running."
}
Write-OK "Device enrolled. State saved to $StatePath"

# ── 5. Install the Windows Service ───────────────────────────────────────────
Write-Step "Installing Windows Service '$ServiceName'"
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Warn "Service '$ServiceName' already exists — stopping and reconfiguring."
    if ($existing.Status -eq 'Running') {
        Stop-Service -Name $ServiceName -Force
        Write-OK 'Stopped existing service'
    }
    & sc.exe config $ServiceName binpath= "`"$exePath`"" | Out-Null
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
Write-Host "  Server : $ServerUrl" -ForegroundColor Green
Write-Host "  State  : $StatePath" -ForegroundColor Green
Write-Host "  Logs   : Get-EventLog -LogName Application -Source $ServiceName" -ForegroundColor Green
Write-Host '══════════════════════════════════════════════════════════' -ForegroundColor Green
Write-Host ''
Write-Host 'The agent will check in with the server every' `
           "$HeartbeatIntervalMinutes minutes." -ForegroundColor Yellow
Write-Host "View this device on the server: $ServerUrl/devices" -ForegroundColor Yellow
