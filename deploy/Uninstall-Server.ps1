<#
.SYNOPSIS
    Stops and removes the PatchPlatform Server Windows Service.

.PARAMETER ServiceName
    Windows Service name. Default: PatchPlatformServer

.PARAMETER RemoveFiles
    Also delete the installation directory. Default: false.

.PARAMETER InstallPath
    Installation directory to remove when -RemoveFiles is set.
    Default: C:\PatchPlatform\Server

.PARAMETER RemoveFirewallRule
    Also remove the firewall rule. Default: false.

.PARAMETER ListenPort
    Port used to find the firewall rule. Default: 5000

.EXAMPLE
    .\Uninstall-Server.ps1

.EXAMPLE
    .\Uninstall-Server.ps1 -RemoveFiles -RemoveFirewallRule
#>
#Requires -RunAsAdministrator
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [string]$ServiceName       = 'PatchPlatformServer',
    [switch]$RemoveFiles,
    [string]$InstallPath       = 'C:\PatchPlatform\Server',
    [switch]$RemoveFirewallRule,
    [int]   $ListenPort        = 5000
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

# ── 3. Remove firewall rule ───────────────────────────────────────────────────
if ($RemoveFirewallRule) {
    Write-Step "Removing firewall rule for port $ListenPort"
    $ruleName = "PatchPlatform Server (port $ListenPort)"
    $rule = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
    if ($rule) {
        if ($PSCmdlet.ShouldProcess($ruleName, 'Remove firewall rule')) {
            Remove-NetFirewallRule -DisplayName $ruleName
            Write-OK 'Firewall rule removed'
        }
    } else {
        Write-Warn "Firewall rule '$ruleName' not found."
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
Write-Host '  PatchPlatform Server uninstalled.' -ForegroundColor Green
Write-Host '══════════════════════════════════════════════════════════' -ForegroundColor Green
Write-Host ''
if (-not $RemoveFiles) {
    Write-Host "Installation files remain at: $InstallPath" -ForegroundColor Yellow
    Write-Host 'Run with -RemoveFiles to delete them.' -ForegroundColor Yellow
}
