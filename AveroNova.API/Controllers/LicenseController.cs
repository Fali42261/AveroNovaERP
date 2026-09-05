using AveroNova.Application.DTOs.License;
using AveroNova.Application.Interfaces.License;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AveroNova.API.Controllers;

[ApiController]
[Route("api/license")]
public sealed class LicenseController : ControllerBase
{
    private readonly ILicenseService _licenses;

    public LicenseController(ILicenseService licenses) => _licenses = licenses;

    [HttpPost("initialize")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Initialize([FromBody] LicenseInitializeRequest request, CancellationToken cancellationToken)
    {
        var result = await _licenses.InitializeAsync(request, User, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> Status([FromQuery] string deviceId, CancellationToken cancellationToken)
    {
        var result = await _licenses.GetStatusAsync(User, deviceId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("validate")]
    [Authorize]
    public async Task<IActionResult> Validate([FromBody] LicenseValidateRequest request, CancellationToken cancellationToken)
    {
        var result = await _licenses.ValidateAsync(request, User, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("sync")]
    [Authorize]
    public async Task<IActionResult> Sync([FromBody] LicenseSyncRequest request, CancellationToken cancellationToken)
    {
        var result = await _licenses.SyncAsync(request, User, cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(Application.Common.ApiResult<T> result)
    {
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Data });
    }
}
