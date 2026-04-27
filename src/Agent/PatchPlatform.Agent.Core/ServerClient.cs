using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using PatchPlatform.Shared.Contracts;

namespace PatchPlatform.Agent.Core;

public class ServerClient : IServerClient
{
    private readonly HttpClient _http;
    private readonly IAgentStateStore _stateStore;
    private readonly ILogger<ServerClient> _logger;

    public ServerClient(HttpClient http, IAgentStateStore stateStore, ILogger<ServerClient> logger)
    {
        _http = http;
        _stateStore = stateStore;
        _logger = logger;
    }

    private async Task SetAuthHeaderAsync(CancellationToken ct)
    {
        var state = await _stateStore.LoadAsync(ct);
        if (state != null && state.DeviceId != Guid.Empty)
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", $"{state.DeviceId}:{state.DeviceSecret}");
        }
    }

    public async Task<EnrollResponse> EnrollAsync(EnrollRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/agent/enroll", request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EnrollResponse>(cancellationToken: ct))!;
    }

    public async Task<HeartbeatResponse> HeartbeatAsync(HeartbeatRequest request, CancellationToken ct = default)
    {
        await SetAuthHeaderAsync(ct);
        var response = await _http.PostAsJsonAsync("api/agent/heartbeat", request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HeartbeatResponse>(cancellationToken: ct))!;
    }

    public async Task<PolicyDto?> GetPolicyAsync(CancellationToken ct = default)
    {
        await SetAuthHeaderAsync(ct);
        var response = await _http.GetAsync("api/agent/policy", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PolicyDto>(cancellationToken: ct);
    }

    public async Task<CatalogSnapshotEnvelopeDto?> GetCatalogSnapshotAsync(string? currentETag = null, CancellationToken ct = default)
    {
        await SetAuthHeaderAsync(ct);
        var request = new HttpRequestMessage(HttpMethod.Get, "api/catalog/snapshot");
        if (currentETag != null)
            request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(currentETag));
        var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotModified) return null;
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CatalogSnapshotEnvelopeDto>(cancellationToken: ct);
    }

    public async Task UploadInventoryAsync(InventoryUploadDto inventory, CancellationToken ct = default)
    {
        await SetAuthHeaderAsync(ct);
        var response = await _http.PostAsJsonAsync("api/agent/inventory", inventory, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task UploadJobResultAsync(JobResultUploadDto result, CancellationToken ct = default)
    {
        await SetAuthHeaderAsync(ct);
        var response = await _http.PostAsJsonAsync("api/agent/jobresult", result, ct);
        response.EnsureSuccessStatusCode();
    }
}
