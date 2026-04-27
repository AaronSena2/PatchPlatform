<#
.SYNOPSIS
    Stops and removes the PatchPlatform Agent Windows Service.

.PARAMETER ServiceName
    Windows Service name. Default: PatchPlatformAgent

.PARAMETER RemoveFiles
    Also delete the installation directory. Default: false.

.PARAMETER InstallPath
    Installation directory to remove when -RemoveFiles is set.
    Default: C:\PatchPlatform\Agent

.PARAMETER RemoveStateFile
    Also delete the agent state file (deviceId + secret). Default: false.

.PARAMETER StatePath
    Path to the state file to remove. Default: C:\ProgramData\PatchPlatform\Agent\state.json

.EXAMPLE
    .\Uninstall-Agent.ps1

.EXAMPLE
    .\Uninstall-Agent.ps1 -RemoveFiles -RemoveStateFile
#>
#Requires -RunAsAdministrator
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [string]$ServiceName    = 'PatchPlatformAgent',
    [switch]$RemoveFiles,
    [string]$InstallPath    = 'C:\PatchPlatform\Agent',
    [switch]$RemoveStateFile,
    [string]$StatePath      = 'C:\ProgramData\PatchPlatform\Agent\state.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step([string]$msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-OK([string]$msg)   { Write-Host "    OK: $msg" -ForegroundColor Green }
function Write-Warn([string]$msg) { Write-Host "    WARN: $msg" -ForegroundColor Yellow }

# ── 1. Stop the service ───────────────────────────────────────────────────────
Write-Step "Stopping service '$ServiceName'"
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $svc) {
    Write-Warn "Service '$ServiceName' not found — nothing to stop."
} elseif ($svc.Status -eq 'Running') {
    Stop-Service -Name $ServiceName -Force
    Write-OK 'Service stopped'
} else {
    Write-OK "Service was already stopped ($($svc.Status))"
}

# ── 2. Remove the service ─────────────────────────────────────────────────────
Write-Step "Removing service '$ServiceName'"
if ($svc) {
    if ($PSCmdlet.ShouldProcess($ServiceName, 'Remove Windows Service')) {
        & sc.exe delete $ServiceName | Out-Null
        Write-OK 'Service removed'
    }
} else {
    Write-Warn 'Service not found — skipping removal.'
}

# ── 3. Remove agent state file ────────────────────────────────────────────────
if ($RemoveStateFile) {
    Write-Step "Removing state file '$StatePath'"
    if (Test-Path $StatePath) {
        if ($PSCmdlet.ShouldProcess($StatePath, 'Delete state file')) {
            Remove-Item -Path $StatePath -Force
            Write-OK 'State file removed'
        }
    } else {
        Write-Warn "State file '$StatePath' not found."
    }
}

# ── 4. Remove installation files ─────────────────────────────────────────────
if ($RemoveFiles) {
    Write-Step "Removing installation directory '$InstallPath'"
    if (Test-Path $InstallPath) {
        if ($PSCmdlet.ShouldProcess($InstallPath, 'Delete directory')) {
            Remove-Item -Path $InstallPath -Recurse -Force
            Write-OK 'Installation directory removed'
        }
    } else {
        Write-Warn "Directory '$InstallPath' not found."
    }
}

# ── Done ──────────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '══════════════════════════════════════════════════════════' -ForegroundColor Green
Write-Host '  PatchPlatform Agent uninstalled.' -ForegroundColor Green
Write-Host '══════════════════════════════════════════════════════════' -ForegroundColor Green
Write-Host ''
if (-not $RemoveFiles) {
    Write-Host "Installation files remain at: $InstallPath" -ForegroundColor Yellow
    Write-Host 'Run with -RemoveFiles to delete them.' -ForegroundColor Yellow
}
if (-not $RemoveStateFile) {
    Write-Host "State file remains at: $StatePath" -ForegroundColor Yellow
    Write-Host 'Run with -RemoveStateFile to delete it.' -ForegroundColor Yellow
}
