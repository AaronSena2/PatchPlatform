using Microsoft.AspNetCore.Mvc;
using PatchPlatform.Server.Application;
using PatchPlatform.Shared.Contracts;

namespace PatchPlatform.Server.Web.Controllers;

[ApiController]
[Route("api/catalog")]
public class CatalogController : ControllerBase
{
    private readonly ICatalogService _catalog;
    private readonly IDeviceAuthService _auth;

    public CatalogController(ICatalogService catalog, IDeviceAuthService auth)
    {
        _catalog = catalog;
        _auth = auth;
    }

    [HttpGet("snapshot")]
    public async Task<IActionResult> GetSnapshot(CancellationToken ct)
    {
        var device = await _auth.AuthenticateAsync(Request.Headers.Authorization.ToString(), ct);
        if (device == null) return Unauthorized(new ApiErrorDto("UNAUTHORIZED", "Invalid credentials"));

        var snapshot = await _catalog.GetCurrentSnapshotAsync(ct);
        if (snapshot == null) return NotFound(new ApiErrorDto("NOT_FOUND", "No catalog published yet"));

        var etag = $"\"{snapshot.Version}\"";
        if (Request.Headers.IfNoneMatch == etag)
            return StatusCode(304);

        Response.Headers.ETag = etag;
        return Ok(snapshot);
    }
}
