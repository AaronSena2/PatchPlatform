# PatchPlatform

An on-prem Windows third-party app patch management platform built with .NET 8, ASP.NET Core Blazor Server, EF Core + SQL Server, and a Windows Service agent.

---

## Downloads

### Compiled release packages (self-contained win-x64)

Built automatically by CI on every version tag and attached to GitHub Releases:

> **[⬇ Download latest release](https://github.com/AaronSena2/PatchPlatform/releases/latest)**

| Package | Contents |
|---|---|
| `PatchPlatform-Server-win-x64.zip` | Server executable + `Install-Server.ps1` + `Uninstall-Server.ps1` |
| `PatchPlatform-Agent-win-x64.zip` | Agent executable + `Install-Agent.ps1` + `Uninstall-Agent.ps1` |

### Install scripts (latest source)

Download individual PowerShell scripts directly from the repository:

| Script | Download |
|---|---|
| `Install-Server.ps1` | [⬇ Download](https://raw.githubusercontent.com/AaronSena2/PatchPlatform/main/deploy/Install-Server.ps1) |
| `Uninstall-Server.ps1` | [⬇ Download](https://raw.githubusercontent.com/AaronSena2/PatchPlatform/main/deploy/Uninstall-Server.ps1) |
| `Install-Agent.ps1` | [⬇ Download](https://raw.githubusercontent.com/AaronSena2/PatchPlatform/main/deploy/Install-Agent.ps1) |
| `Uninstall-Agent.ps1` | [⬇ Download](https://raw.githubusercontent.com/AaronSena2/PatchPlatform/main/deploy/Uninstall-Agent.ps1) |

> **Tip:** Use the release packages for production. Use the raw scripts when deploying from a source build (`dotnet publish` on the target machine or a build server).

---

## Prerequisites

| Requirement | Version |
|---|---|
| .NET SDK | 8.0 |
| SQL Server Express or LocalDB | 2019 or later |
| Windows (for Agent) | Windows 10/Server 2019+ |

Install .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0

Install SQL Server Express (free): https://www.microsoft.com/en-us/sql-server/sql-server-downloads

LocalDB (for dev) ships with Visual Studio or can be installed via the SQL Server Express installer.

---

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
    PatchPlatform.Agent.Core/           AgentRuntime, ServerClient, InventoryScanner, StateStore
    PatchPlatform.Agent.Service/        .NET Worker running as Windows Service
tests/
  PatchPlatform.Server.Tests/           Unit tests: catalog signer
  PatchPlatform.Agent.Tests/            Unit tests: maintenance window parsing
```

---

## How to Run the Server

### 1. Configure the database connection

Edit `src/Server/PatchPlatform.Server.Web/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=PatchPlatformDev;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "PatchPlatform": {
    "PackageStorePath": "C:\\PatchPlatform\\Packages",
    "CatalogSigningSecret": "CHANGE_ME_IN_PRODUCTION_USE_A_LONG_RANDOM_SECRET"
  }
}
```

> **Note:** For production, use a full SQL Server Express connection string, e.g.:
> `Server=.\\SQLEXPRESS;Database=PatchPlatform;Trusted_Connection=True;`

### 2. Apply EF Core migrations (first run)

Migrations are applied automatically on startup via `db.Database.Migrate()`.

To manually apply or create migrations:
```bash
cd src/Server/PatchPlatform.Server.Web
dotnet ef database update --project ../PatchPlatform.Server.Data
```

### 3. Run the server

```bash
cd src/Server/PatchPlatform.Server.Web
dotnet run
```

The server starts on `http://localhost:5000` (or the port shown in console output).

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

### Via curl
```bash
# No token needed for POST /api/tokens — use the UI
# But enrollment is done by agent using the token
```

---

## How to Enroll the Agent

### Prerequisites
Build the agent:
```bash
cd src/Agent/PatchPlatform.Agent.Service
dotnet build
```

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
