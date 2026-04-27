# PatchPlatform

An on-prem Windows third-party app patch management platform built with .NET 8, ASP.NET Core Blazor Server, EF Core + SQL Server, and a Windows Service agent.

---

## Downloads

### Compiled release packages (self-contained win-x64)

Built automatically by CI on every version tag and attached to GitHub Releases:

> **[⬇ Download latest release](https://github.com/AaronSena2/PatchPlatform/releases/latest)**

| Package | Contents |
|---|---|
| `PatchPlatform-Server-win-x64.zip` | `PatchPlatform.Server.Web.exe` (single-file, self-contained) + `Install-Server.ps1` + `Uninstall-Server.ps1` |
| `PatchPlatform-Agent-win-x64.zip` | `PatchPlatform.Agent.Service.exe` (single-file, self-contained) + `Install-Agent.ps1` + `Uninstall-Agent.ps1` |

### Install scripts (main branch)

| Script | Download |
|---|---|
| `Install-Server.ps1` | [⬇ Download](https://raw.githubusercontent.com/AaronSena2/PatchPlatform/main/deploy/Install-Server.ps1) |
| `Uninstall-Server.ps1` | [⬇ Download](https://raw.githubusercontent.com/AaronSena2/PatchPlatform/main/deploy/Uninstall-Server.ps1) |
| `Install-Agent.ps1` | [⬇ Download](https://raw.githubusercontent.com/AaronSena2/PatchPlatform/main/deploy/Install-Agent.ps1) |
| `Uninstall-Agent.ps1` | [⬇ Download](https://raw.githubusercontent.com/AaronSena2/PatchPlatform/main/deploy/Uninstall-Agent.ps1) |

> **Tip:** Use the release packages for production. Use the install scripts with `-SkipPublish` when you have pre-built artifacts, or without `-SkipPublish` to have the scripts run `dotnet publish` on the target machine.

---

## Prerequisites

| Requirement | Details |
|---|---|
| .NET 8 SDK | Required on the machine running the install scripts (server + agent build) |
| SQL Server Express or LocalDB | 2019 or later; SQL Express for production, LocalDB for dev |
| Windows OS | Windows 10 / Windows Server 2019 or later (agent and server) |
| PowerShell | 5.1 or later (included in Windows 10/Server 2016+) |
| Administrator rights | Required by the install scripts to create Windows Services and firewall rules |

### Installing prerequisites

**Install .NET 8 SDK**
```
https://dotnet.microsoft.com/download/dotnet/8.0
```

**Install SQL Server Express (free)**
```
https://www.microsoft.com/en-us/sql-server/sql-server-downloads
```
Choose "Express" edition. During setup, note the instance name (default: `.\SQLEXPRESS`).

**LocalDB for local development** ships with Visual Studio or can be installed via the SQL Server Express installer (select "LocalDB" feature). Connection string: `Server=(localdb)\mssqllocaldb;Database=PatchPlatformDev;Trusted_Connection=True;`

---

## Install from Source (Enterprise / Script-based)

This is the primary deployment method: the install scripts clone/copy the source, then run `dotnet publish` on the target machine.

### Step 1 — Clone the repository on the target server

```powershell
if (Test-Path C:\src\PatchPlatform) {
    git -C C:\src\PatchPlatform pull
} else {
    git clone https://github.com/AaronSena2/PatchPlatform.git C:\src\PatchPlatform
}
cd C:\src\PatchPlatform
```

### Step 2 — Install the Server

Open PowerShell **as Administrator** and run:

> **Note:** If PowerShell blocks the script with an "execution policy" error, run once as Administrator:
> ```powershell
> Set-ExecutionPolicy RemoteSigned -Scope LocalMachine
> ```

```powershell
cd C:\src\PatchPlatform
.\deploy\Install-Server.ps1 `
    -SourcePath "C:\src\PatchPlatform" `
    -SqlConnectionString "Server=.\SQLEXPRESS;Database=PatchPlatform;Trusted_Connection=True;TrustServerCertificate=True;"
```

The script will:
1. Verify that `dotnet` and the solution file are present
2. Run `dotnet publish` to build and publish the server
3. Write a production `appsettings.json` (connection string, ports, signing secret)
4. Apply EF Core database migrations automatically on first startup
5. Create and start the `PatchPlatformServer` Windows Service
6. Open the firewall port (default: 5000)

**Common parameters:**

| Parameter | Default | Description |
|---|---|---|
| `-SourcePath` | Auto-detected from script location | Repo root containing `PatchPlatform.slnx` |
| `-InstallPath` | `C:\ProgramData\PatchPlatform\Server` | Where the server binary is deployed |
| `-PackageStorePath` | `C:\ProgramData\PatchPlatform\Packages` | Where package files are stored |
| `-SqlConnectionString` | SQL Express local | Connection string for the database |
| `-ListenPort` | `5000` | HTTP port |
| `-SigningSecret` | Auto-generated | HMAC secret for catalog signing (record this!) |
| `-SkipPublish` | `$false` | Skip `dotnet publish` (use pre-built artifacts) |

**Re-running** the script is safe — it stops, reconfigures, and restarts the existing service.

### Step 3 — Verify the server is healthy

```powershell
Invoke-RestMethod http://localhost:5000/health
```

Expected response:
```json
{ "status": "ok", "version": "1.0.0.0", "utc": "2025-01-01T00:00:00+00:00" }
```

### Step 4 — Generate an enrollment token

Open the admin UI: `http://localhost:5000/tokens`

Or use curl:
```bash
# Use the admin UI at /tokens to generate a token — no auth required for prototype
```

### Step 5 — Install the Agent on each managed machine

Open PowerShell **as Administrator** on the managed machine and run:

```powershell
.\deploy\Install-Agent.ps1 `
    -SourcePath "C:\src\PatchPlatform" `
    -ServerUrl "http://patchserver:5000" `
    -EnrollmentToken "YOUR_TOKEN_HERE"
```

The script will:
1. Check server reachability (non-blocking; warns if unreachable)
2. Run `dotnet publish` to build the agent
3. Write a production `appsettings.json`
4. Run device enrollment (stores `deviceId` and `deviceSecret` in the state file)
5. Create and start the `PatchPlatformAgent` Windows Service

**Common parameters:**

| Parameter | Default | Description |
|---|---|---|
| `-ServerUrl` | **Required** | Base URL of the PatchPlatform Server |
| `-EnrollmentToken` | **Required** | One-time token from the server admin UI |
| `-SourcePath` | Auto-detected | Repo root containing `PatchPlatform.slnx` |
| `-InstallPath` | `C:\ProgramData\PatchPlatform\Agent` | Where the agent binary is deployed |
| `-StatePath` | `C:\ProgramData\PatchPlatform\Agent\state.json` | Agent state file (deviceId + secret) |
| `-HeartbeatIntervalMinutes` | `60` | How often the agent checks in |
| `-SkipPublish` | `$false` | Skip `dotnet publish` |

**Re-running** the script is safe — it re-enrolls and restarts the service.

---

## Typical Failures and Fixes

| Symptom | Likely Cause | Fix |
|---|---|---|
| `dotnet: command not found` | .NET SDK not installed or not on PATH | Install .NET 8 SDK; re-open the terminal |
| `PatchPlatform.slnx missing` | Wrong `-SourcePath` or script not run from repo root | Pass `-SourcePath` explicitly pointing to the repo root |
| `Failed to apply database migrations` (server log) | SQL Server not running, wrong instance name, or connection string wrong | Check SQL Server service is running; verify instance name in connection string |
| `Login failed for user` (SQL) | SQL auth issue | Use Windows auth (`Trusted_Connection=True`) or create a SQL login |
| `Could not open a connection to SQL Server` | SQL Server port/pipe blocked | Check firewall, enable TCP/IP in SQL Server Configuration Manager |
| `Enrollment failed (exit 1)` | Token expired, invalid, or max-use exhausted | Generate a new token from `/tokens` on the server |
| `Server did not respond at /health` (agent install warning) | Server not yet started or wrong URL | Ensure server service is running; check port in `-ServerUrl` |
| Service starts then stops immediately | Missing appsettings, wrong paths, or exception at startup | Check Windows Event Viewer > Application log for the error |



## Solution Structure

```
PatchPlatform.slnx
src/
  Shared/
    PatchPlatform.Shared.Contracts/     DTOs shared between server and agent
    PatchPlatform.Shared.Crypto/        HMAC signing, SHA-256 hashing, maintenance window evaluator
  Server/
    PatchPlatform.Server.Domain/        EF Core entities
    PatchPlatform.Server.Data/          ServerDbContext + migrations
    PatchPlatform.Server.Application/   Use-case services (enrollment, auth, policy, catalog, inventory, jobs)
    PatchPlatform.Server.Web/           ASP.NET Core + Blazor Server UI + REST API controllers
  Agent/
    PatchPlatform.Agent.Core/           AgentRuntime, ServerClient, RegistryInventoryScanner, StateStore
    PatchPlatform.Agent.Service/        .NET Worker running as Windows Service
tests/
  PatchPlatform.Server.Tests/           Unit tests: catalog signer
  PatchPlatform.Agent.Tests/            Unit tests: maintenance window parsing
deploy/
  Install-Server.ps1                    Server install script (from source)
  Install-Agent.ps1                     Agent install script (from source)
  Uninstall-Server.ps1                  Server uninstall script
  Uninstall-Agent.ps1                   Agent uninstall script
```

---

## How to Run the Server (Development)

### 1. Configure the database connection

Edit `src/Server/PatchPlatform.Server.Web/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=PatchPlatformDev;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "PatchPlatform": {
    "PackageStorePath": "C:\\ProgramData\\PatchPlatform\\Packages",
    "CatalogSigningSecret": "CHANGE_ME_IN_PRODUCTION_USE_A_LONG_RANDOM_SECRET"
  }
}
```

> **Note:** For production, use a full SQL Server Express connection string:
> `Server=.\\SQLEXPRESS;Database=PatchPlatform;Trusted_Connection=True;TrustServerCertificate=True;`

### 2. Run the server

```bash
cd src/Server/PatchPlatform.Server.Web
dotnet run
```

EF Core migrations are applied automatically on startup. If the database does not exist, it will be created. If the connection string is invalid or the server is unreachable, the error is logged clearly and startup continues (the `/health` endpoint will still respond).

To manually apply or create migrations:
```bash
dotnet ef database update --project src/Server/PatchPlatform.Server.Data --startup-project src/Server/PatchPlatform.Server.Web
```

The server starts on `http://localhost:5000` (or the port shown in console output).

### 3. Verify health

```bash
curl http://localhost:5000/health
```

---

## Admin UI Pages

| Page | URL | Description |
|---|---|---|
| Home | `/` | Welcome page |
| Enrollment Tokens | `/tokens` | Generate one-time enrollment tokens |
| Devices | `/devices` | List enrolled devices + last seen |
| Job Results | `/jobs` | View recent patch job results |
| Policy Editor | `/policy` | Edit maintenance window + auto-approve settings |

---

## How to Create an Enrollment Token

### Via the Admin UI
1. Open `http://localhost:5000/tokens`
2. Set expiry date/time and max uses
3. Click **Generate Token**
4. Copy the token shown

---

## How to Enroll the Agent

### Enroll (one-time)
```bash
dotnet run --project src/Agent/PatchPlatform.Agent.Service -- \
  --enroll \
  --token <YOUR_TOKEN_HERE> \
  --server http://localhost:5000
```

This stores the `deviceId` and `deviceSecret` in the state file (default: `state.json` in the working directory).

### Run as foreground service (dev)
```bash
dotnet run --project src/Agent/PatchPlatform.Agent.Service
```

### Install as Windows Service (production)
```powershell
sc.exe create PatchPlatformAgent `
  binPath= "C:\Path\To\PatchPlatform.Agent.Service.exe" `
  start= auto
sc.exe start PatchPlatformAgent
```

Configure the state path and server URL in `appsettings.json`:
```json
{
  "Agent": {
    "ServerBaseUrl": "http://your-server:5000",
    "StatePath": "C:\\ProgramData\\PatchPlatform\\Agent\\state.json",
    "HeartbeatIntervalMinutes": 60
  }
}
```

---

## Sample curl Commands

### Enroll a device
```bash
curl -X POST http://localhost:5000/api/agent/enroll \
  -H "Content-Type: application/json" \
  -d '{
    "token": "YOUR_TOKEN",
    "machineName": "WORKSTATION-01",
    "osVersion": "Windows 11 22H2",
    "agentVersion": "1.0.0"
  }'
```
Response: `{"deviceId": "...", "deviceSecret": "..."}`

### Heartbeat
```bash
curl -X POST http://localhost:5000/api/agent/heartbeat \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <deviceId>:<deviceSecret>" \
  -d '{
    "deviceId": "DEVICE_ID",
    "currentPolicyVersion": 0,
    "currentCatalogVersion": 0,
    "agentVersion": "1.0.0"
  }'
```

### Get policy
```bash
curl http://localhost:5000/api/agent/policy \
  -H "Authorization: Bearer <deviceId>:<deviceSecret>"
```

### Get catalog snapshot
```bash
curl http://localhost:5000/api/catalog/snapshot \
  -H "Authorization: Bearer <deviceId>:<deviceSecret>"
```

### Upload inventory
```bash
curl -X POST http://localhost:5000/api/agent/inventory \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <deviceId>:<deviceSecret>" \
  -d '{
    "deviceId": "DEVICE_ID",
    "scannedAtUtc": "2024-01-15T03:00:00Z",
    "apps": [
      {"name": "7-Zip 23.01", "version": "23.01", "publisher": "Igor Pavlov", "installDate": "20240101"}
    ]
  }'
```

### Upload job result
```bash
curl -X POST http://localhost:5000/api/agent/jobresult \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <deviceId>:<deviceSecret>" \
  -d '{
    "deviceId": "DEVICE_ID",
    "jobId": "NEW_JOB_GUID",
    "startedAtUtc": "2024-01-15T03:00:00Z",
    "completedAtUtc": "2024-01-15T03:05:00Z",
    "items": [
      {"appId": "7zip", "appName": "7-Zip", "status": "Success", "errorMessage": null, "completedAtUtc": "2024-01-15T03:05:00Z"}
    ]
  }'
```

---

## Static Package Content Serving

The server serves files from the `PackageStorePath` folder under `/content/...`.

To serve a package file, place it in the package store folder and access it at:
`http://localhost:5000/content/my-app-installer.exe`

---

## Running Tests

```bash
dotnet test PatchPlatform.slnx
```

Tests cover:
- **Maintenance window parsing** (daily, weekday ranges, midnight-crossing, day-of-week lists)
- **Catalog envelope HMAC verification** (valid/invalid signatures, wrong secrets, hash consistency)

---

## Architecture Notes

### Authentication
Agent auth uses `Authorization: Bearer <deviceId>:<deviceSecret>`.
The server stores only the SHA-256 hash of the secret. The plaintext secret is sent once during enrollment and never stored on the server.

### Catalog Integrity
Catalog payloads are signed with HMAC-SHA256 using the server's `CatalogSigningSecret`. Agents can verify the signature using the shared Crypto library before trusting catalog contents.

### Maintenance Windows
Format: `HH:mm-HH:mm` (daily) or `Mon-Fri HH:mm-HH:mm` (weekday range) or `Sat,Sun HH:mm-HH:mm` (specific days). Midnight-crossing windows (e.g., `23:00-01:00`) are supported.

### TODO / Not Yet Implemented
- Real installer execution (stubbed as dry-run)
- Authenticode signature verification for downloaded files
- Content mirroring/download from vendor URLs
- DPAPI-protected secret storage on agent (uses plain JSON file for prototype)
- ASP.NET Core Identity for admin UI authentication
- Hangfire background jobs for catalog refresh
