using Microsoft.EntityFrameworkCore;
using PatchPlatform.Server.Domain;

namespace PatchPlatform.Server.Data;

public class ServerDbContext : DbContext
{
    public ServerDbContext(DbContextOptions<ServerDbContext> options) : base(options) { }

    public DbSet<EnrollmentToken> EnrollmentTokens => Set<EnrollmentToken>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceSecret> DeviceSecrets => Set<DeviceSecret>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<CatalogVersion> CatalogVersions => Set<CatalogVersion>();
    public DbSet<DeviceInventory> DeviceInventories => Set<DeviceInventory>();
    public DbSet<UpdateJob> UpdateJobs => Set<UpdateJob>();
    public DbSet<UpdateJobItem> UpdateJobItems => Set<UpdateJobItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>()
            .HasOne(d => d.Secret)
            .WithOne(s => s.Device)
            .HasForeignKey<DeviceSecret>(s => s.DeviceId);

        modelBuilder.Entity<Device>()
            .HasMany(d => d.Inventories)
            .WithOne(i => i.Device)
            .HasForeignKey(i => i.DeviceId);

        modelBuilder.Entity<Device>()
            .HasMany(d => d.Jobs)
            .WithOne(j => j.Device)
            .HasForeignKey(j => j.DeviceId);

        modelBuilder.Entity<UpdateJob>()
            .HasMany(j => j.Items)
            .WithOne(i => i.Job)
            .HasForeignKey(i => i.JobId);

        modelBuilder.Entity<Policy>()
            .HasData(new Policy
            {
                Id = 1,
                Version = 1,
                Name = "Default",
                MaintenanceWindowExpression = "02:00-04:00",
                MaintenanceWindowDurationMinutes = 120,
                AutoApprove = true,
                RawJson = "{\"maintenanceWindow\":\"02:00-04:00\",\"autoApprove\":true}",
                UpdatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
            });
    }
}
