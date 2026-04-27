using Microsoft.EntityFrameworkCore;
using PatchPlatform.Server.Data;
using PatchPlatform.Server.Domain;
using PatchPlatform.Shared.Contracts;
using PatchPlatform.Shared.Crypto;
using System.Text.Json;

namespace PatchPlatform.Server.Application;

public interface IEnrollmentService
{
    Task<string> CreateTokenAsync(DateTimeOffset expiresAt, int maxUses, CancellationToken ct = default);
    Task<EnrollResponse> EnrollDeviceAsync(EnrollRequest request, CancellationToken ct = default);
}

public interface IDeviceAuthService
{
    Task<Device?> AuthenticateAsync(string authHeader, CancellationToken ct = default);
}

public interface IPolicyService
{
    Task<PolicyDto?> GetCurrentPolicyAsync(CancellationToken ct = default);
    Task<Policy> UpdatePolicyAsync(string name, string maintenanceWindowExpression, int durationMinutes, bool autoApprove, string rawJson, CancellationToken ct = default);
}

public interface ICatalogService
{
    Task<CatalogSnapshotEnvelopeDto?> GetCurrentSnapshotAsync(CancellationToken ct = default);
    Task<CatalogVersion> PublishSnapshotAsync(string payloadJson, CancellationToken ct = default);
}

public interface IInventoryService
{
    Task IngestAsync(InventoryUploadDto dto, CancellationToken ct = default);
}

public interface IJobResultService
{
    Task IngestAsync(JobResultUploadDto dto, CancellationToken ct = default);
}

public class EnrollmentService : IEnrollmentService
{
    private readonly ServerDbContext _db;
    public EnrollmentService(ServerDbContext db) => _db = db;

    public async Task<string> CreateTokenAsync(DateTimeOffset expiresAt, int maxUses, CancellationToken ct = default)
    {
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        _db.EnrollmentTokens.Add(new EnrollmentToken
        {
            Token = token,
            ExpiresAt = expiresAt,
            MaxUses = maxUses
        });
        await _db.SaveChangesAsync(ct);
        return token;
    }

    public async Task<EnrollResponse> EnrollDeviceAsync(EnrollRequest request, CancellationToken ct = default)
    {
        var tokenEntity = await _db.EnrollmentTokens
            .FirstOrDefaultAsync(t => t.Token == request.Token && t.IsActive, ct)
            ?? throw new InvalidOperationException("Invalid or expired enrollment token");

        if (tokenEntity.ExpiresAt < DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Enrollment token has expired");

        if (tokenEntity.UsedCount >= tokenEntity.MaxUses)
            throw new InvalidOperationException("Enrollment token has been used the maximum number of times");

        tokenEntity.UsedCount++;
        if (tokenEntity.UsedCount >= tokenEntity.MaxUses)
            tokenEntity.IsActive = false;

        var secret = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var device = new Device
        {
            MachineName = request.MachineName,
            OsVersion = request.OsVersion,
            AgentVersion = request.AgentVersion,
            LastSeenAt = DateTimeOffset.UtcNow
        };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync(ct);

        _db.DeviceSecrets.Add(new DeviceSecret
        {
            DeviceId = device.Id,
            SecretHash = CatalogSigner.HashSecret(secret)
        });
        await _db.SaveChangesAsync(ct);

        return new EnrollResponse(device.Id, secret);
    }
}

public class DeviceAuthService : IDeviceAuthService
{
    private readonly ServerDbContext _db;
    public DeviceAuthService(ServerDbContext db) => _db = db;

    public async Task<Device?> AuthenticateAsync(string authHeader, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = authHeader.Substring(7);
        var parts = token.Split(':', 2);
        if (parts.Length != 2) return null;

        if (!Guid.TryParse(parts[0], out var deviceId)) return null;
        var secret = parts[1];

        var device = await _db.Devices
            .Include(d => d.Secret)
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.IsActive, ct);

        if (device?.Secret == null) return null;

        var hash = CatalogSigner.HashSecret(secret);
        if (!string.Equals(hash, device.Secret.SecretHash, StringComparison.Ordinal))
            return null;

        device.LastSeenAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return device;
    }
}

public class PolicyService : IPolicyService
{
    private readonly ServerDbContext _db;
    public PolicyService(ServerDbContext db) => _db = db;

    public async Task<PolicyDto?> GetCurrentPolicyAsync(CancellationToken ct = default)
    {
        var policy = await _db.Policies.OrderByDescending(p => p.Version).FirstOrDefaultAsync(ct);
        if (policy == null) return null;
        return new PolicyDto(policy.Version, policy.MaintenanceWindowExpression, policy.MaintenanceWindowDurationMinutes, policy.AutoApprove, policy.RawJson);
    }

    public async Task<Policy> UpdatePolicyAsync(string name, string maintenanceWindowExpression, int durationMinutes, bool autoApprove, string rawJson, CancellationToken ct = default)
    {
        var current = await _db.Policies.OrderByDescending(p => p.Version).FirstOrDefaultAsync(ct);
        var newPolicy = new Policy
        {
            Name = name,
            Version = (current?.Version ?? 0) + 1,
            MaintenanceWindowExpression = maintenanceWindowExpression,
            MaintenanceWindowDurationMinutes = durationMinutes,
            AutoApprove = autoApprove,
            RawJson = rawJson,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.Policies.Add(newPolicy);
        await _db.SaveChangesAsync(ct);
        return newPolicy;
    }
}

public class CatalogService : ICatalogService
{
    private readonly ServerDbContext _db;
    private readonly string _signingSecret;

    public CatalogService(ServerDbContext db, string signingSecret)
    {
        _db = db;
        _signingSecret = signingSecret;
    }

    public async Task<CatalogSnapshotEnvelopeDto?> GetCurrentSnapshotAsync(CancellationToken ct = default)
    {
        var catalog = await _db.CatalogVersions.Where(c => c.IsCurrent).OrderByDescending(c => c.Version).FirstOrDefaultAsync(ct);
        if (catalog == null) return null;
        return JsonSerializer.Deserialize<CatalogSnapshotEnvelopeDto>(catalog.EnvelopeJson);
    }

    public async Task<CatalogVersion> PublishSnapshotAsync(string payloadJson, CancellationToken ct = default)
    {
        var current = await _db.CatalogVersions.Where(c => c.IsCurrent).FirstOrDefaultAsync(ct);
        if (current != null) current.IsCurrent = false;

        var version = (await _db.CatalogVersions.MaxAsync(c => (int?)c.Version, ct) ?? 0) + 1;
        var signature = CatalogSigner.ComputeHmac(payloadJson, _signingSecret);
        var envelope = new CatalogSnapshotEnvelopeDto(version, payloadJson, signature, DateTimeOffset.UtcNow);
        var envelopeJson = JsonSerializer.Serialize(envelope);
        var etag = CatalogSigner.ComputeSha256(envelopeJson);

        var catalogVersion = new CatalogVersion
        {
            Version = version,
            EnvelopeJson = envelopeJson,
            ETag = etag,
            IsCurrent = true
        };
        _db.CatalogVersions.Add(catalogVersion);
        await _db.SaveChangesAsync(ct);
        return catalogVersion;
    }
}

public class InventoryService : IInventoryService
{
    private readonly ServerDbContext _db;
    public InventoryService(ServerDbContext db) => _db = db;

    public async Task IngestAsync(InventoryUploadDto dto, CancellationToken ct = default)
    {
        var inventoryJson = JsonSerializer.Serialize(dto);
        _db.DeviceInventories.Add(new DeviceInventory
        {
            DeviceId = dto.DeviceId,
            InventoryJson = inventoryJson,
            ReceivedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }
}

public class JobResultService : IJobResultService
{
    private readonly ServerDbContext _db;
    public JobResultService(ServerDbContext db) => _db = db;

    public async Task IngestAsync(JobResultUploadDto dto, CancellationToken ct = default)
    {
        var job = new UpdateJob
        {
            Id = dto.JobId,
            DeviceId = dto.DeviceId,
            CreatedAt = dto.StartedAtUtc,
            Status = "Completed"
        };
        _db.UpdateJobs.Add(job);
        await _db.SaveChangesAsync(ct);

        foreach (var item in dto.Items)
        {
            _db.UpdateJobItems.Add(new UpdateJobItem
            {
                JobId = dto.JobId,
                AppId = item.AppId,
                AppName = item.AppName,
                Status = item.Status,
                ErrorMessage = item.ErrorMessage,
                CompletedAt = item.CompletedAtUtc
            });
        }
        await _db.SaveChangesAsync(ct);
    }
}
