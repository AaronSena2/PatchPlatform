namespace PatchPlatform.Server.Domain;

public class EnrollmentToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public int MaxUses { get; set; } = 1;
    public int UsedCount { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}

public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string MachineName { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;
    public DateTimeOffset EnrolledAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastSeenAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DeviceSecret? Secret { get; set; }
    public List<DeviceInventory> Inventories { get; set; } = new();
    public List<UpdateJob> Jobs { get; set; } = new();
}

public class DeviceSecret
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public string SecretHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Device? Device { get; set; }
}

public class Policy
{
    public int Id { get; set; }
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Default";
    public string MaintenanceWindowExpression { get; set; } = "02:00-04:00";
    public int MaintenanceWindowDurationMinutes { get; set; } = 120;
    public bool AutoApprove { get; set; } = true;
    public string RawJson { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class CatalogVersion
{
    public int Id { get; set; }
    public int Version { get; set; }
    public string EnvelopeJson { get; set; } = string.Empty;
    public string ETag { get; set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsCurrent { get; set; } = false;
}

public class DeviceInventory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public string InventoryJson { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public Device? Device { get; set; }
}

public class UpdateJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "Pending";
    public Device? Device { get; set; }
    public List<UpdateJobItem> Items { get; set; } = new();
}

public class UpdateJobItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public string AppId { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? ErrorMessage { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public UpdateJob? Job { get; set; }
}
