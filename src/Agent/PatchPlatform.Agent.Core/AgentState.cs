namespace PatchPlatform.Agent.Core;

public class AgentState
{
    public Guid DeviceId { get; set; }
    public string DeviceSecret { get; set; } = string.Empty;
    public string ServerBaseUrl { get; set; } = string.Empty;
    public int LastPolicyVersion { get; set; } = 0;
    public int LastCatalogVersion { get; set; } = 0;
    public DateTimeOffset? LastInventoryUpload { get; set; }
}
