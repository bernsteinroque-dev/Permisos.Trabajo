using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PTS_Synthon.Data;
using PTS_Synthon.Models;
using PTS_Synthon.Services;

namespace PTS_Synthon.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PermisosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;
    private readonly IConfiguration _config;
    private readonly ILogger<PermisosController> _logger;

    public PermisosController(AppDbContext db, IEmailService email, IConfiguration config, ILogger<PermisosController> logger)
    {
        _db = db;
        _email = email;
        _config = config;
        _logger = logger;
    }

    private string GetCurrentUser()
    {
        var devAuth = _config.GetSection("DevAuth");
        if (devAuth.GetValue<bool>("Enabled") &&
            (User.Identity == null || !User.Identity.IsAuthenticated))
            return devAuth.GetValue<string>("WindowsUser") ?? "DEV\\devuser";
        return User.Identity?.Name ?? "unknown";
    }

    private string GetCurrentRole()
    {
        var devAuth = _config.GetSection("DevAuth");
        if (devAuth.GetValue<bool>("Enabled") &&
            (User.Identity == null || !User.Identity.IsAuthenticated))
            return devAuth.GetValue<string>("Role") ?? "admin";

        var adSettings = _config.GetSection("AdSettings");
        if (User.IsInRole(adSettings.GetValue<string>("AdminGroup") ?? "PTS_Admins")) return "admin";
        if (User.IsInRole(adSettings.GetValue<string>("SupervisorGroup") ?? "PTS_Supervisores")) return "supervisor";
        if (User.IsInRole(adSettings.GetValue<string>("ProveedorGroup") ?? "PTS_Proveedores")) return "proveedor";
        if (User.IsInRole(adSettings.GetValue<string>("LecturaGroup") ?? "PTS_Lectura")) return "lectura";
        return "denied";
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? tipo,
        [FromQuery] string? desde,
        [FromQuery] string? hasta,
        [FromQuery] string? search)
    {
        var currentUser = GetCurrentUser();
        var role = GetCurrentRole();

        var query = _db.Permisos.AsQueryable();

        if (role == "proveedor")
            query = query.Where(p => p.CreadoPor == currentUser);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(p => p.Estado == status);

        if (!string.IsNullOrEmpty(tipo))
            query = query.Where(p => p.Tipo == tipo);

        if (!string.IsNullOrEmpty(desde) && DateTime.TryParse(desde, out var desdeDate))
            query = query.Where(p => p.FechaCreacion >= desdeDate);

        if (!string.IsNullOrEmpty(hasta) && DateTime.TryParse(hasta, out var hastaDate))
            query = query.Where(p => p.FechaCreacion <= hastaDate.AddDays(1));

        if (!string.IsNullOrEmpty(search))
        {
            var lower = search.ToLower();
            query = query.Where(p =>
                (p.Descripcion != null && p.Descripcion.ToLower().Contains(lower)) ||
                (p.ProveedorNombre != null && p.ProveedorNombre.ToLower().Contains(lower)) ||
                p.NumeroPermiso.ToString().Contains(search));
        }

        var permisos = await query.OrderByDescending(p => p.FechaCreacion).ToListAsync();
        return Ok(permisos);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var p = await _db.Permisos.FindAsync(id);
        if (p == null) return NotFound();

        var currentUser = GetCurrentUser();
        var role = GetCurrentRole();
        if (role == "proveedor" && p.CreadoPor != currentUser)
            return Forbid();

        return Ok(p);
    }

    [HttpGet("buscar/{numero}")]
    public async Task<IActionResult> BuscarPorNumero(string numero)
    {
        if (!int.TryParse(numero, out var n))
            return Ok((object?)null);

        var p = await _db.Permisos.FirstOrDefaultAsync(x => x.NumeroPermiso == n);
        return Ok(p);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Permiso permiso)
    {
        var role = GetCurrentRole();
        if (role == "lectura" || role == "supervisor")
            return Forbid();

        var currentUser = GetCurrentUser();
        permiso.Id = 0;
        permiso.CreadoPor = currentUser;
        permiso.FechaCreacion = DateTime.UtcNow;
        permiso.Estado = "pending";
        permiso.EmailNotificado = false;

        _db.Permisos.Add(permiso);
        await _db.SaveChangesAsync();

        // Send notification email
        var supervisorEmail = _config.GetValue<string>("NotificacionSettings:SupervisorEmail") ?? "";
        if (!string.IsNullOrEmpty(supervisorEmail))
        {
            try
            {
                await _email.SendNuevoPermisoAsync(permiso, supervisorEmail);
                permiso.EmailNotificado = true;
                await _db.SaveChangesAsync();
            }
            catch { /* log but don't fail */ }
        }

        return CreatedAtAction(nameof(GetById), new { id = permiso.Id }, permiso);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Permiso updated)
    {
        var existing = await _db.Permisos.FindAsync(id);
        if (existing == null) return NotFound();

        var currentUser = GetCurrentUser();
        var role = GetCurrentRole();

        if (role != "admin" && !(existing.CreadoPor == currentUser && existing.Estado == "pending"))
            return Forbid();

        existing.Tipo = updated.Tipo;
        existing.ProveedorId = updated.ProveedorId;
        existing.ProveedorNombre = updated.ProveedorNombre;
        existing.ProveedorEmail = updated.ProveedorEmail;
        existing.Empresa = updated.Empresa;
        existing.Fecha = updated.Fecha;
        existing.VigenciaDesde = updated.VigenciaDesde;
        existing.VigenciaHasta = updated.VigenciaHasta;
        existing.Orden = updated.Orden;
        existing.Planta = updated.Planta;
        existing.Equipo = updated.Equipo;
        existing.Descripcion = updated.Descripcion;
        existing.RealizaElTrabajo = updated.RealizaElTrabajo;
        existing.VerificacionesJson = updated.VerificacionesJson;
        existing.PeligrosJson = updated.PeligrosJson;
        existing.EppGeneralJson = updated.EppGeneralJson;
        existing.CheckListJson = updated.CheckListJson;
        existing.HerramientaAntichispa = updated.HerramientaAntichispa;
        existing.IluminacionEspecial = updated.IluminacionEspecial;
        existing.GeneraResiduos = updated.GeneraResiduos;
        existing.Consigno = updated.Consigno;
        existing.ConsignoCual = updated.ConsignoCual;
        existing.ConsignoQuien = updated.ConsignoQuien;
        existing.Observaciones = updated.Observaciones;
        existing.Emisor = updated.Emisor;
        existing.Receptor = updated.Receptor;
        existing.Ejecutante1 = updated.Ejecutante1;
        existing.Ejecutante2 = updated.Ejecutante2;
        existing.Ejecutante3 = updated.Ejecutante3;
        existing.Ejecutante4 = updated.Ejecutante4;
        existing.TerminacionTrabajo = updated.TerminacionTrabajo;
        existing.LimpiezaSector = updated.LimpiezaSector;
        existing.TermFirma = updated.TermFirma;
        existing.RecepcionEmisor = updated.RecepcionEmisor;
        existing.RecepcionFirma = updated.RecepcionFirma;
        existing.ModificadoPor = currentUser;
        existing.FechaModificacion = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpPut("{id:int}/aprobar")]
    public async Task<IActionResult> Aprobar(int id, [FromBody] AprobacionRequest req)
    {
        var existing = await _db.Permisos.FindAsync(id);
        if (existing == null) return NotFound();

        var role = GetCurrentRole();
        if (role != "admin" && role != "supervisor") return Forbid();

        existing.Estado = "approved";
        existing.SupervisorNombre = req.SupervisorNombre;
        existing.SupervisorComentario = req.SupervisorComentario;
        existing.ModificadoPor = GetCurrentUser();
        existing.FechaModificacion = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(existing.ProveedorEmail))
        {
            try { await _email.SendPermisoAprobadoAsync(existing); } catch { }
        }

        return Ok(existing);
    }

    [HttpPut("{id:int}/rechazar")]
    public async Task<IActionResult> Rechazar(int id, [FromBody] AprobacionRequest req)
    {
        var existing = await _db.Permisos.FindAsync(id);
        if (existing == null) return NotFound();

        var role = GetCurrentRole();
        if (role != "admin" && role != "supervisor") return Forbid();

        existing.Estado = "rejected";
        existing.SupervisorNombre = req.SupervisorNombre;
        existing.SupervisorComentario = req.SupervisorComentario;
        existing.ModificadoPor = GetCurrentUser();
        existing.FechaModificacion = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(existing.ProveedorEmail))
        {
            try { await _email.SendPermisoRechazadoAsync(existing); } catch { }
        }

        return Ok(existing);
    }

    [HttpPost("{id:int}/email")]
    public async Task<IActionResult> SendEmail(int id)
    {
        var p = await _db.Permisos.FindAsync(id);
        if (p == null) return NotFound();

        var errors = new List<string>();

        var supervisorEmail = _config.GetValue<string>("NotificacionSettings:SupervisorEmail") ?? "";
        if (!string.IsNullOrEmpty(supervisorEmail))
        {
            try { await _email.SendNuevoPermisoAsync(p, supervisorEmail); }
            catch (Exception ex) { errors.Add($"Supervisor: {ex.Message}"); _logger.LogError(ex, "Error sending email to supervisor"); }
        }

        if (!string.IsNullOrEmpty(p.ProveedorEmail))
        {
            try { await _email.SendNuevoPermisoAsync(p, p.ProveedorEmail); }
            catch (Exception ex) { errors.Add($"Proveedor: {ex.Message}"); _logger.LogError(ex, "Error sending email to proveedor"); }
        }

        if (errors.Any()) return StatusCode(500, new { errors });

        p.EmailNotificado = true;
        await _db.SaveChangesAsync();
        return Ok(new { sent = true });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var role = GetCurrentRole();
        if (role != "admin") return Forbid();

        var p = await _db.Permisos.FindAsync(id);
        if (p == null) return NotFound();

        _db.Permisos.Remove(p);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public record AprobacionRequest(string SupervisorNombre, string? SupervisorComentario);
