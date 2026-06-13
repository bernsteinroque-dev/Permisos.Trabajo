using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PTS_Synthon.Data;

namespace PTS_Synthon.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;

    public AuthController(IConfiguration config, AppDbContext db)
    {
        _config = config;
        _db = db;
    }

    [HttpGet("health")]
    [AllowAnonymous]
    public async Task<IActionResult> Health()
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync();
            var permisosCount = canConnect ? await _db.Permisos.CountAsync() : -1;
            var proveedoresCount = canConnect ? await _db.Proveedores.CountAsync() : -1;
            return Ok(new
            {
                status = canConnect ? "ok" : "db_error",
                database = canConnect,
                permisos = permisosCount,
                proveedores = proveedoresCount,
                serverTime = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return Ok(new { status = "error", message = ex.Message });
        }
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
            return Ok(new { isAuthenticated = false, role = "unauthenticated" });

        var adSettings = _config.GetSection("AdSettings");
        var adminGroup = adSettings.GetValue<string>("AdminGroup") ?? "PTS_Admins";
        var supervisorGroup = adSettings.GetValue<string>("SupervisorGroup") ?? "PTS_Supervisores";
        var proveedorGroup = adSettings.GetValue<string>("ProveedorGroup") ?? "PTS_Proveedores";
        var lecturaGroup = adSettings.GetValue<string>("LecturaGroup") ?? "PTS_Lectura";
        var defaultRole = adSettings.GetValue<string>("DefaultRole") ?? "admin";

        string role = defaultRole;
        if (User.IsInRole(adminGroup)) role = "admin";
        else if (User.IsInRole(supervisorGroup)) role = "supervisor";
        else if (User.IsInRole(proveedorGroup)) role = "proveedor";
        else if (User.IsInRole(lecturaGroup)) role = "lectura";

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
