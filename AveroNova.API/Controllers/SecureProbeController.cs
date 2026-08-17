using AveroNova.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AveroNova.API.Controllers;

/// <summary>Authorization probe endpoints for Phase 3 verification (401/403).</summary>
[ApiController]
[Route("api/secure")]
public sealed class SecureProbeController : ControllerBase
{
    [HttpGet("ping")]
    [Authorize]
    public IActionResult Ping() => Ok(new { success = true, message = "authenticated" });

    [HttpGet("users-manage")]
    [RequirePermission("Users.Manage")]
    public IActionResult UsersManage() => Ok(new { success = true, permission = "Users.Manage" });

    [HttpGet("impossible")]
    [RequirePermission("Does.Not.Exist")]
    public IActionResult Impossible() => Ok(new { success = true });
}
