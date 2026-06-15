using System.DirectoryServices.AccountManagement;
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

    [HttpPost("login")]
    [AllowAnonymous]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { error = "missing_fields", message = "Ingresá usuario y contraseña." });

        var adSettings = _config.GetSection("AdSettings");
        var domain = adSettings.GetValue<string>("Domain") ?? "SYNTHON";

        // Strip domain prefix if present (DOMAIN\user or user@domain)
        var username = req.Username.Contains('\\')
            ? req.Username.Split('\\').Last()
            : req.Username.Contains('@') ? req.Username.Split('@').First() : req.Username;

        try
        {
            using var ctx = new PrincipalContext(ContextType.Domain, domain);

            // Step 1: check user exists
            UserPrincipal? user = null;
            try { user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, username); }
            catch { }

            if (user == null)
                return Unauthorized(new { error = "user_not_found", message = "El usuario no existe en el dominio." });

            // Step 2: validate password
            bool passwordOk = false;
            try { passwordOk = ctx.ValidateCredentials(username, req.Password); }
            catch { }

            if (!passwordOk)
                return Unauthorized(new { error = "invalid_password", message = "La contraseña ingresada es incorrecta." });

            // Step 3: check group membership
            var adminGroup    = adSettings.GetValue<string>("AdminGroup")      ?? "PTS_Admins";
            var supervisorGroup = adSettings.GetValue<string>("SupervisorGroup") ?? "PTS_Supervisores";
            var proveedorGroup  = adSettings.GetValue<string>("ProveedorGroup")  ?? "PTS_Proveedores";
            var lecturaGroup    = adSettings.GetValue<string>("LecturaGroup")    ?? "PTS_Lectura";

            string role = "denied";
            try
            {
                var groups = user.GetAuthorizationGroups().Select(g => g.Name).ToHashSet();
                if (groups.Contains(adminGroup))       role = "admin";
                else if (groups.Contains(supervisorGroup)) role = "supervisor";
                else if (groups.Contains(proveedorGroup))  role = "proveedor";
                else if (groups.Contains(lecturaGroup))    role = "lectura";
            }
            catch { }

            if (role == "denied")
                return StatusCode(403, new { error = "no_group", message = "El usuario no pertenece a ningún grupo autorizado de la aplicación." });

            var displayName = user.DisplayName ?? user.SamAccountName ?? username;
            var windowsUser = $"{domain}\\{username}";

            // Store in session
            HttpContext.Session.SetString("windowsUser", windowsUser);
            HttpContext.Session.SetString("displayName", displayName);
            HttpContext.Session.SetString("role", role);

            return Ok(new { name = displayName, windowsUser, role, isAuthenticated = true });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "ad_unavailable", message = $"No se puede conectar con el dominio: {ex.Message}" });
        }
    }

    [HttpGet("validate-supervisor")]
    [AllowAnonymous]
    public IActionResult ValidateSupervisor([FromQuery] string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return BadRequest(new { error = "missing_username" });

        var adSettings = _config.GetSection("AdSettings");
        var domain = adSettings.GetValue<string>("Domain") ?? "ar.corporate.synthon-group.com";
        var supervisorGroup = adSettings.GetValue<string>("SupervisorGroup") ?? "PTS_Supervisores";
        var adminGroup      = adSettings.GetValue<string>("AdminGroup")      ?? "PTS_Admins";

        var sam = username.Contains('\\') ? username.Split('\\').Last()
                : username.Contains('@')  ? username.Split('@').First()
                : username;

        try
        {
            using var ctx = new PrincipalContext(ContextType.Domain, domain);

            UserPrincipal? user = null;
            try { user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, sam); } catch { }

            if (user == null)
                return Ok(new { valid = false, error = "user_not_found", message = "Usuario inexistente en el dominio." });

            bool isSup = false;
            try
            {
                var groups = user.GetAuthorizationGroups().Select(g => g.Name).ToHashSet();
                isSup = groups.Contains(supervisorGroup) || groups.Contains(adminGroup);
            }
            catch { }

            if (!isSup)
                return Ok(new { valid = false, error = "not_supervisor", message = "El usuario no pertenece al grupo de Supervisores." });

            var displayName = user.DisplayName ?? user.SamAccountName ?? sam;
            return Ok(new { valid = true, displayName });
        }
        catch (Exception ex)
        {
            return Ok(new { valid = false, error = "ad_unavailable", message = $"No se pudo contactar el dominio: {ex.Message}" });
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Ok(new { ok = true });
    }

    [HttpGet("me")]
    [AllowAnonymous]
    public IActionResult GetMe()
    {
        // Check session first
        var sessionRole = HttpContext.Session.GetString("role");
        if (!string.IsNullOrEmpty(sessionRole))
        {
            return Ok(new
            {
                name        = HttpContext.Session.GetString("displayName") ?? "",
                windowsUser = HttpContext.Session.GetString("windowsUser") ?? "",
                role        = sessionRole,
                isAuthenticated = true
            });
        }

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

        // Windows Auth fallback
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            var adSettings = _config.GetSection("AdSettings");
            var adminGroup      = adSettings.GetValue<string>("AdminGroup")      ?? "PTS_Admins";
            var supervisorGroup = adSettings.GetValue<string>("SupervisorGroup") ?? "PTS_Supervisores";
            var proveedorGroup  = adSettings.GetValue<string>("ProveedorGroup")  ?? "PTS_Proveedores";
            var lecturaGroup    = adSettings.GetValue<string>("LecturaGroup")    ?? "PTS_Lectura";
            var defaultRole     = adSettings.GetValue<string>("DefaultRole")     ?? "denied";

            string role = defaultRole;
            if (User.IsInRole(adminGroup))       role = "admin";
            else if (User.IsInRole(supervisorGroup)) role = "supervisor";
            else if (User.IsInRole(proveedorGroup))  role = "proveedor";
            else if (User.IsInRole(lecturaGroup))    role = "lectura";

            var windowsUser = User.Identity.Name ?? string.Empty;
            var displayName = windowsUser.Contains('\\') ? windowsUser.Split('\\').Last() : windowsUser;

            if (role == "denied")
                return Ok(new { isAuthenticated = false, role = "denied", errorMessage = "El usuario no pertenece a ningún grupo autorizado." });

            return Ok(new { name = displayName, windowsUser, role, isAuthenticated = true });
        }

        return Ok(new { isAuthenticated = false, role = "unauthenticated" });
    }
}

public record LoginRequest(string Username, string Password);
