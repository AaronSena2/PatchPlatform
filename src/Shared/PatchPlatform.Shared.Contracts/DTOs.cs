namespace PatchPlatform.Shared.Contracts;

public record EnrollRequest(string Token, string MachineName, string OsVersion, string AgentVersion);
public record EnrollResponse(Guid DeviceId, string DeviceSecret);

public record HeartbeatRequest(Guid DeviceId, int CurrentPolicyVersion, int CurrentCatalogVersion, string AgentVersion);
public record HeartbeatResponse(int PolicyVersion, int CatalogVersion, bool RequestInventory);

public record PolicyDto(int Version, string MaintenanceWindowCron, int MaintenanceWindowDurationMinutes, bool AutoApprove, string RawJson);

public record CatalogSnapshotEnvelopeDto(int Version, string Payload, string Signature, DateTimeOffset GeneratedAtUtc);

public record AppManifestDto(string Id, string Name, string Version, string InstallerUrl, string InstallerHash, string SilentArgs, DetectionRuleDto Detection);
public record DetectionRuleDto(string Type, string Path, string Value);

public record InstalledAppDto(string Name, string Version, string Publisher, string InstallDate);
public record InventoryUploadDto(Guid DeviceId, DateTimeOffset ScannedAtUtc, List<InstalledAppDto> Apps);

public record JobItemResultDto(string AppId, string AppName, string Status, string? ErrorMessage, DateTimeOffset CompletedAtUtc);
public record JobResultUploadDto(Guid DeviceId, Guid JobId, DateTimeOffset StartedAtUtc, DateTimeOffset CompletedAtUtc, List<JobItemResultDto> Items);

public record GenerateTokenRequest(DateTimeOffset ExpiresAt, int MaxUses);
public record GenerateTokenResponse(string Token);

public record ApiErrorDto(string Code, string Message, string? Details = null);
