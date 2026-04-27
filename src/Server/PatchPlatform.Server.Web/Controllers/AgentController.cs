using Microsoft.AspNetCore.Mvc;
using PatchPlatform.Server.Application;
using PatchPlatform.Shared.Contracts;

namespace PatchPlatform.Server.Web.Controllers;

[ApiController]
[Route("api/agent")]
public class AgentController : ControllerBase
{
    private readonly IEnrollmentService _enrollment;
    private readonly IDeviceAuthService _auth;
    private readonly IPolicyService _policy;
    private readonly ICatalogService _catalog;
    private readonly IInventoryService _inventory;
    private readonly IJobResultService _jobResult;

    public AgentController(
        IEnrollmentService enrollment,
        IDeviceAuthService auth,
        IPolicyService policy,
        ICatalogService catalog,
        IInventoryService inventory,
        IJobResultService jobResult)
    {
        _enrollment = enrollment;
        _auth = auth;
        _policy = policy;
        _catalog = catalog;
        _inventory = inventory;
        _jobResult = jobResult;
    }

    [HttpPost("enroll")]
    public async Task<IActionResult> Enroll([FromBody] EnrollRequest request, CancellationToken ct)
    {
        try
        {
            var response = await _enrollment.EnrollDeviceAsync(request, ct);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorDto("ENROLL_FAILED", ex.Message));
        }
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest request, CancellationToken ct)
    {
        var device = await _auth.AuthenticateAsync(Request.Headers.Authorization.ToString(), ct);
        if (device == null) return Unauthorized(new ApiErrorDto("UNAUTHORIZED", "Invalid credentials"));

        var policy = await _policy.GetCurrentPolicyAsync(ct);
        var catalog = await _catalog.GetCurrentSnapshotAsync(ct);

        return Ok(new HeartbeatResponse(
            PolicyVersion: policy?.Version ?? 0,
            CatalogVersion: catalog?.Version ?? 0,
            RequestInventory: false
        ));
    }

    [HttpGet("policy")]
    public async Task<IActionResult> GetPolicy(CancellationToken ct)
    {
        var device = await _auth.AuthenticateAsync(Request.Headers.Authorization.ToString(), ct);
        if (device == null) return Unauthorized(new ApiErrorDto("UNAUTHORIZED", "Invalid credentials"));

        var policy = await _policy.GetCurrentPolicyAsync(ct);
        if (policy == null) return NotFound(new ApiErrorDto("NOT_FOUND", "No policy configured"));
        return Ok(policy);
    }

    [HttpPost("inventory")]
    public async Task<IActionResult> PostInventory([FromBody] InventoryUploadDto dto, CancellationToken ct)
    {
        var device = await _auth.AuthenticateAsync(Request.Headers.Authorization.ToString(), ct);
        if (device == null) return Unauthorized(new ApiErrorDto("UNAUTHORIZED", "Invalid credentials"));

        await _inventory.IngestAsync(dto, ct);
        return Ok();
    }

    [HttpPost("jobresult")]
    public async Task<IActionResult> PostJobResult([FromBody] JobResultUploadDto dto, CancellationToken ct)
    {
        var device = await _auth.AuthenticateAsync(Request.Headers.Authorization.ToString(), ct);
        if (device == null) return Unauthorized(new ApiErrorDto("UNAUTHORIZED", "Invalid credentials"));

        await _jobResult.IngestAsync(dto, ct);
        return Ok();
    }
}
