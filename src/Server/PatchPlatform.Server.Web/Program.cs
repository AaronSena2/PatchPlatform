using Microsoft.EntityFrameworkCore;
using PatchPlatform.Server.Application;
using PatchPlatform.Server.Data;

var builder = WebApplication.CreateBuilder(args);

// Support running as a Windows Service (sets correct ContentRoot automatically)
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "PatchPlatform Server";
});

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddDbContext<ServerDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<IDeviceAuthService, DeviceAuthService>();
builder.Services.AddScoped<IPolicyService, PolicyService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IJobResultService, JobResultService>();
builder.Services.AddScoped<ICatalogService>(sp =>
    new CatalogService(
        sp.GetRequiredService<ServerDbContext>(),
        builder.Configuration["PatchPlatform:CatalogSigningSecret"] ?? "dev-secret"));

builder.Services.AddControllers();

var app = builder.Build();

// ── Apply EF Core migrations on startup ──────────────────────────────────────
// Safe for prototype; gate behind a config flag for production if desired.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
    db.Database.Migrate();
    app.Logger.LogInformation("Database migrations applied successfully.");
}
catch (Exception ex)
{
    app.Logger.LogError(ex,
        "Failed to apply database migrations. " +
        "Verify that the connection string '{ConnectionString}' is correct and the database server is reachable.",
        builder.Configuration.GetConnectionString("DefaultConnection"));
    // Do not abort startup — server can still serve the health endpoint and UI pages.
}

// ── PackageStorePath: resolve to absolute path first, then ensure it exists ──
var rawPackageStorePath = builder.Configuration["PatchPlatform:PackageStorePath"] ?? "packages";
var absolutePackagePath = Path.IsPathRooted(rawPackageStorePath)
    ? rawPackageStorePath
    : Path.Combine(app.Environment.ContentRootPath, rawPackageStorePath);

if (!Directory.Exists(absolutePackagePath))
{
    Directory.CreateDirectory(absolutePackagePath);
    app.Logger.LogInformation("Created package store directory: {Path}", absolutePackagePath);
}

app.UseStaticFiles();
app.UseRouting();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(absolutePackagePath),
    RequestPath = "/content"
});

// ── Health endpoint (unauthenticated) ─────────────────────────────────────────
var serverVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    version = serverVersion,
    utc = DateTimeOffset.UtcNow
})).AllowAnonymous();

app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
