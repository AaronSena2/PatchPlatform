using Microsoft.EntityFrameworkCore;
using PatchPlatform.Server.Application;
using PatchPlatform.Server.Data;

var builder = WebApplication.CreateBuilder(args);

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

app.UseStaticFiles();
app.UseRouting();

var packageStorePath = builder.Configuration["PatchPlatform:PackageStorePath"] ?? "packages";
if (!Directory.Exists(packageStorePath))
    Directory.CreateDirectory(packageStorePath);

var absolutePackagePath = Path.IsPathRooted(packageStorePath)
    ? packageStorePath
    : Path.Combine(app.Environment.ContentRootPath, packageStorePath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(absolutePackagePath),
    RequestPath = "/content"
});

app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
