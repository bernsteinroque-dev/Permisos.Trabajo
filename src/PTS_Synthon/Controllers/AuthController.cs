using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PTS_Synthon.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;

    public AuthController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("me")]
    [AllowAnonymous]
    public IActionResult GetMe()
    {
        // Development fallback
        var devAuth = _config.GetSection("DevAuth");
        if (devAuth.GetValue<bool>("Enabled") &&
            (User.Identity == null || !User.Identity.IsAuthenticated))
        {
            var devRole = devAuth.GetValue<string>("Role") ?? "admin";
            return Ok(new
            {
                name = devAuth.GetValue<string>("UserName") ?? "Dev User",
                windowsUser = devAuth.GetValue<string>("WindowsUser") ?? "DEV\\devuser",
                role = devRole,
                isAuthenticated = true
            });
        }

        if (User.Identity == null || !User.Identity.IsAuthenticated)
            return Unauthorized(new { isAuthenticated = false });

        var adSettings = _config.GetSection("AdSettings");
        var adminGroup = adSettings.GetValue<string>("AdminGroup") ?? "PTS_Admins";
        var supervisorGroup = adSettings.GetValue<string>("SupervisorGroup") ?? "PTS_Supervisores";
        var proveedorGroup = adSettings.GetValue<string>("ProveedorGroup") ?? "PTS_Proveedores";

        string role = "lectura";
        if (User.IsInRole(adminGroup)) role = "admin";
        else if (User.IsInRole(supervisorGroup)) role = "supervisor";
        else if (User.IsInRole(proveedorGroup)) role = "proveedor";

        var windowsUser = User.Identity.Name ?? string.Empty;
        var displayName = windowsUser.Contains('\\')
            ? windowsUser.Split('\\').Last()
            : windowsUser;

        return Ok(new
        {
            name = displayName,
            windowsUser = windowsUser,
            role = role,
            isAuthenticated = true
        });
    }
}
