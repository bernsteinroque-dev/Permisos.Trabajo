using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PTS_Synthon.Data;
using PTS_Synthon.Models;

namespace PTS_Synthon.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProveedoresController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public ProveedoresController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
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
        return adSettings.GetValue<string>("DefaultRole") ?? "lectura";
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var proveedores = await _db.Proveedores.OrderBy(p => p.RazonSocial).ToListAsync();
        return Ok(proveedores);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var p = await _db.Proveedores
            .Include(x => x.Permisos)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        return Ok(p);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Proveedor proveedor)
    {
        var role = GetCurrentRole();
        if (role != "admin" && role != "supervisor") return Forbid();

        proveedor.Id = 0;
        proveedor.CreadoPor = GetCurrentUser();
        proveedor.FechaCreacion = DateTime.UtcNow;

        _db.Proveedores.Add(proveedor);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = proveedor.Id }, proveedor);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Proveedor updated)
    {
        var role = GetCurrentRole();
        if (role != "admin" && role != "supervisor") return Forbid();

        var existing = await _db.Proveedores.FindAsync(id);
        if (existing == null) return NotFound();

        existing.RazonSocial = updated.RazonSocial;
        existing.CUIT = updated.CUIT;
        existing.Rubro = updated.Rubro;
        existing.Estado = updated.Estado;
        existing.Habilitacion = updated.Habilitacion;
        existing.Contacto = updated.Contacto;
        existing.Telefono = updated.Telefono;
        existing.Email = updated.Email;
        existing.Cargo = updated.Cargo;
        existing.VencART = updated.VencART;
        existing.VencRC = updated.VencRC;
        existing.VencHabMunicipal = updated.VencHabMunicipal;
        existing.VencAFIP = updated.VencAFIP;
        existing.VencSegHigiene = updated.VencSegHigiene;
        existing.VencOtros = updated.VencOtros;
        existing.ObsDocumentacion = updated.ObsDocumentacion;
        existing.Observaciones = updated.Observaciones;

        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var role = GetCurrentRole();
        if (role != "admin") return Forbid();

        var p = await _db.Proveedores.FindAsync(id);
        if (p == null) return NotFound();

        _db.Proveedores.Remove(p);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
