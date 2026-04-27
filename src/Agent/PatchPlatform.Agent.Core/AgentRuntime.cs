using Microsoft.Extensions.Logging;
using PatchPlatform.Shared.Contracts;
using PatchPlatform.Shared.Crypto;

namespace PatchPlatform.Agent.Core;

public class AgentRuntime
{
    private readonly IServerClient _server;
    private readonly IAgentStateStore _stateStore;
    private readonly IInventoryScanner _inventoryScanner;
    private readonly ILogger<AgentRuntime> _logger;

    public AgentRuntime(
        IServerClient server,
        IAgentStateStore stateStore,
        IInventoryScanner inventoryScanner,
        ILogger<AgentRuntime> logger)
    {
        _server = server;
        _stateStore = stateStore;
        _inventoryScanner = inventoryScanner;
        _logger = logger;
    }

    public async Task EnrollAsync(string token, string serverUrl, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting enrollment with server {ServerUrl}", serverUrl);
        var request = new EnrollRequest(
            Token: token,
            MachineName: Environment.MachineName,
            OsVersion: Environment.OSVersion.ToString(),
            AgentVersion: "1.0.0"
        );
        var response = await _server.EnrollAsync(request, ct);
        var state = new AgentState
        {
            DeviceId = response.DeviceId,
            DeviceSecret = response.DeviceSecret,
            ServerBaseUrl = serverUrl
        };
        await _stateStore.SaveAsync(state, ct);
        _logger.LogInformation("Enrolled successfully as device {DeviceId}", response.DeviceId);
    }

    public async Task RunCheckInAsync(CancellationToken ct = default)
    {
        var state = await _stateStore.LoadAsync(ct);
        if (state == null || state.DeviceId == Guid.Empty)
        {
            _logger.LogWarning("Agent not enrolled. Run with --enroll to enroll first.");
            return;
        }

        _logger.LogInformation("Running check-in for device {DeviceId}", state.DeviceId);

        var heartbeat = await _server.HeartbeatAsync(
            new HeartbeatRequest(state.DeviceId, state.LastPolicyVersion, state.LastCatalogVersion, "1.0.0"),
            ct);

        if (heartbeat.PolicyVersion > state.LastPolicyVersion)
        {
            _logger.LogInformation("New policy version {Version} available, fetching", heartbeat.PolicyVersion);
            var policy = await _server.GetPolicyAsync(ct);
            if (policy != null)
            {
                state.LastPolicyVersion = policy.Version;
                _logger.LogInformation("Policy updated to version {Version}", policy.Version);
            }
        }

        if (heartbeat.CatalogVersion > state.LastCatalogVersion)
        {
            _logger.LogInformation("New catalog version {Version} available, fetching", heartbeat.CatalogVersion);
            var catalog = await _server.GetCatalogSnapshotAsync(ct: ct);
            if (catalog != null)
            {
                state.LastCatalogVersion = catalog.Version;
                _logger.LogInformation("Catalog updated to version {Version}", catalog.Version);
            }
        }

        if (heartbeat.RequestInventory || state.LastInventoryUpload == null ||
            (DateTimeOffset.UtcNow - state.LastInventoryUpload.Value).TotalHours > 24)
        {
            await UploadInventoryAsync(state, ct);
        }

        await _stateStore.SaveAsync(state, ct);
    }

    public async Task RunMaintenanceWindowAsync(PolicyDto policy, CancellationToken ct = default)
    {
        var state = await _stateStore.LoadAsync(ct);
        if (state == null) return;

        if (!MaintenanceWindowHelper.IsInWindow(policy.MaintenanceWindowCron, DateTimeOffset.UtcNow))
        {
            _logger.LogInformation("Outside maintenance window. Skipping.");
            return;
        }

        _logger.LogInformation("In maintenance window. Running dry-run update plan.");

        var jobId = Guid.NewGuid();
        var result = new JobResultUploadDto(
            DeviceId: state.DeviceId,
            JobId: jobId,
            StartedAtUtc: DateTimeOffset.UtcNow,
            CompletedAtUtc: DateTimeOffset.UtcNow.AddMinutes(1),
            Items: new List<JobItemResultDto>
            {
                new("stub-app-1", "Stub App A", "DryRun", null, DateTimeOffset.UtcNow)
            }
        );
        await _server.UploadJobResultAsync(result, ct);
        _logger.LogInformation("Dry-run job result uploaded for job {JobId}", jobId);
    }

    private async Task UploadInventoryAsync(AgentState state, CancellationToken ct)
    {
        _logger.LogInformation("Uploading inventory for device {DeviceId}", state.DeviceId);
        var apps = await _inventoryScanner.ScanAsync(ct);
        var inventory = new InventoryUploadDto(state.DeviceId, DateTimeOffset.UtcNow, apps);
        await _server.UploadInventoryAsync(inventory, ct);
        state.LastInventoryUpload = DateTimeOffset.UtcNow;
        _logger.LogInformation("Inventory uploaded ({Count} apps)", apps.Count);
    }
}
