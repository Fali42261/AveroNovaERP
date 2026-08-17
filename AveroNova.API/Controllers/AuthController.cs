using System.Security.Claims;
using AveroNova.Application.DTOs.Auth;
using AveroNova.Application.Interfaces.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AveroNova.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Phase 2 registration (required for login verification).</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _auth.RegisterAsync(request, cancellationToken);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, error = result.Error, errors = result.Errors });
        return Ok(new { success = true, data = result.Data });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _auth.LoginAsync(request, cancellationToken);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Data });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await _auth.RefreshAsync(request, cancellationToken);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Data });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request, CancellationToken cancellationToken)
    {
        var result = await _auth.LogoutAsync(User, request ?? new LogoutRequest(), cancellationToken);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var result = await _auth.GetMeAsync(User, cancellationToken);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Data });
    }
}
