using HobbyShopApp.DTOs;
using HobbyShopApp.Models;
using HobbyShopApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HobbyShopApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var (response, error) = await _auth.RegisterAsync(request);
        if (error is not null)
            return Conflict(new { message = error });

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var (response, error) = await _auth.LoginAsync(request);
        if (error is not null)
            return Unauthorized(new { message = error });

        return Ok(response);
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            id = User.FindFirst("sub")?.Value,
            email = User.FindFirst("email")?.Value,
            role = User.FindFirst("role")?.Value,
            permissions = User.FindAll("permission").Select(c => c.Value)
        });
    }

    [Authorize(Policy = PermissionCodes.UserManageAdmin)]
    [HttpGet("admin-check")]
    public IActionResult AdminCheck()
    {
        return Ok(new { message = "Bạn có quyền quản lý Staff và Admin." });
    }
}