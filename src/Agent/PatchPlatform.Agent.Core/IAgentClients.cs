using PatchPlatform.Shared.Contracts;

namespace PatchPlatform.Agent.Core;

public interface IServerClient
{
    Task<EnrollResponse> EnrollAsync(EnrollRequest request, CancellationToken ct = default);
    Task<HeartbeatResponse> HeartbeatAsync(HeartbeatRequest request, CancellationToken ct = default);
    Task<PolicyDto?> GetPolicyAsync(CancellationToken ct = default);
    Task<CatalogSnapshotEnvelopeDto?> GetCatalogSnapshotAsync(string? currentETag = null, CancellationToken ct = default);
    Task UploadInventoryAsync(InventoryUploadDto inventory, CancellationToken ct = default);
    Task UploadJobResultAsync(JobResultUploadDto result, CancellationToken ct = default);
}
