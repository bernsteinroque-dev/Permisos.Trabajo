using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PTS_Synthon.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PTS_Synthon.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReportesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var permisos = await _db.Permisos.ToListAsync();
        var proveedores = await _db.Proveedores.ToListAsync();

        return Ok(new
        {
            total = permisos.Count,
            pending = permisos.Count(p => p.Estado == "pending"),
            approved = permisos.Count(p => p.Estado == "approved"),
            rejected = permisos.Count(p => p.Estado == "rejected"),
            byTipo = new
            {
                pts = permisos.Count(p => p.Tipo == "pts"),
                caliente = permisos.Count(p => p.Tipo == "caliente"),
                altura = permisos.Count(p => p.Tipo == "altura")
            },
            totalProveedores = proveedores.Count,
            proveedoresActivos = proveedores.Count(p => p.Estado == "Activo")
        });
    }

    [HttpGet("csv")]
    public async Task<IActionResult> ExportCsv()
    {
        var permisos = await _db.Permisos.OrderByDescending(p => p.FechaCreacion).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("N°,Tipo,Estado,Proveedor,Empresa,Fecha,Descripción,Creado Por,Fecha Creación,Supervisor,Comentario");

        foreach (var p in permisos)
        {
            sb.AppendLine(string.Join(",",
                p.NumeroPermiso,
                $"\"{p.Tipo}\"",
                $"\"{p.Estado}\"",
                $"\"{p.ProveedorNombre ?? ""}\"",
                $"\"{p.Empresa ?? ""}\"",
                $"\"{p.Fecha ?? ""}\"",
                $"\"{(p.Descripcion ?? "").Replace("\"", "\"\"")}\"",
                $"\"{p.CreadoPor}\"",
                p.FechaCreacion.ToString("yyyy-MM-dd"),
                $"\"{p.SupervisorNombre ?? ""}\"",
                $"\"{(p.SupervisorComentario ?? "").Replace("\"", "\"\"")}\""));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"permisos_{DateTime.Now:yyyyMMdd}.csv");
    }

    [HttpGet("vencimientos")]
    public async Task<IActionResult> GetVencimientos()
    {
        var hoy = DateTime.Today;
        var limite = hoy.AddDays(30);

        var proveedores = await _db.Proveedores.ToListAsync();
        var alertas = new List<object>();

        foreach (var p in proveedores)
        {
            var docs = new List<object>();
            CheckVenc(p.VencART, "ART", hoy, limite, docs);
            CheckVenc(p.VencRC, "Resp. Civil", hoy, limite, docs);
            CheckVenc(p.VencHabMunicipal, "Hab. Municipal", hoy, limite, docs);
            CheckVenc(p.VencAFIP, "AFIP", hoy, limite, docs);
            CheckVenc(p.VencSegHigiene, "Seg. e Higiene", hoy, limite, docs);
            CheckVenc(p.VencOtros, "Otros", hoy, limite, docs);

            if (docs.Any())
                alertas.Add(new { proveedor = p.RazonSocial, id = p.Id, documentos = docs });
        }

        return Ok(alertas);
    }

    private static void CheckVenc(string? fechaStr, string nombre, DateTime hoy, DateTime limite, List<object> docs)
    {
        if (!string.IsNullOrEmpty(fechaStr) && DateTime.TryParse(fechaStr, out var fecha))
        {
            if (fecha >= hoy && fecha <= limite)
            {
                var dias = (int)(fecha - hoy).TotalDays;
                docs.Add(new { documento = nombre, vencimiento = fechaStr, diasRestantes = dias });
            }
        }
    }

    [HttpPost("pdf/{permisoId:int}")]
    [HttpGet("pdf/{permisoId:int}")]
    public async Task<IActionResult> GeneratePdf(int permisoId)
    {
        var p = await _db.Permisos.FindAsync(permisoId);
        if (p == null) return NotFound();

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(t => t.FontSize(10).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("SYNTHON S.A.")
                            .Bold().FontSize(18).FontColor(Colors.Orange.Darken2);
                        row.ConstantItem(200).AlignRight().Column(c =>
                        {
                            c.Item().Text($"PERMISO DE TRABAJO N° {p.NumeroPermiso}")
                                .Bold().FontSize(12);
                            c.Item().Text($"Tipo: {p.Tipo?.ToUpper() ?? ""}").FontSize(10);
                        });
                    });
                    col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Orange.Darken2);
                });

                page.Content().PaddingTop(15).Column(col =>
                {
                    var statusColor = p.Estado switch
                    {
                        "approved" => Colors.Green.Darken1,
                        "rejected" => Colors.Red.Darken1,
                        _ => Colors.Orange.Medium
                    };
                    var statusText = p.Estado switch
                    {
                        "approved" => "APROBADO",
                        "rejected" => "RECHAZADO",
                        _ => "PENDIENTE"
                    };
                    col.Item().Background(statusColor).Padding(5)
                        .Text(statusText).FontColor(Colors.White).Bold().FontSize(11).AlignCenter();

                    col.Item().PaddingTop(10).Text("DATOS GENERALES").Bold().FontSize(11);
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

                    col.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Proveedor: {p.ProveedorNombre ?? "-"}");
                            c.Item().Text($"Empresa: {p.Empresa ?? "-"}");
                            c.Item().Text($"Fecha: {p.Fecha ?? "-"}");
                            c.Item().Text($"Orden de Trabajo: {p.Orden ?? "-"}");
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Planta: {p.Planta ?? "-"}");
                            c.Item().Text($"Equipo: {p.Equipo ?? "-"}");
                            c.Item().Text($"Vigencia: {p.VigenciaDesde ?? "-"} al {p.VigenciaHasta ?? "-"}");
                            c.Item().Text($"Realiza el Trabajo: {p.RealizaElTrabajo ?? "-"}");
                        });
                    });

                    col.Item().PaddingTop(8).Text("DESCRIPCIÓN DEL TRABAJO").Bold().FontSize(11);
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                    col.Item().PaddingTop(5).Text(p.Descripcion ?? "-");

                    if (!string.IsNullOrEmpty(p.Observaciones))
                    {
                        col.Item().PaddingTop(8).Text("OBSERVACIONES").Bold().FontSize(11);
                        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                        col.Item().PaddingTop(5).Text(p.Observaciones);
                    }

                    col.Item().PaddingTop(10).Text("FIRMAS").Bold().FontSize(11);
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                    col.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Emisor: {p.Emisor ?? "-"}");
                            c.Item().Text($"Receptor: {p.Receptor ?? "-"}");
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Ejecutante 1: {p.Ejecutante1 ?? "-"}");
                            c.Item().Text($"Ejecutante 2: {p.Ejecutante2 ?? "-"}");
                            c.Item().Text($"Ejecutante 3: {p.Ejecutante3 ?? "-"}");
                            c.Item().Text($"Ejecutante 4: {p.Ejecutante4 ?? "-"}");
                        });
                    });

                    if (!string.IsNullOrEmpty(p.SupervisorNombre))
                    {
                        col.Item().PaddingTop(10).Text("SUPERVISIÓN").Bold().FontSize(11);
                        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                        col.Item().PaddingTop(5).Column(c =>
                        {
                            c.Item().Text($"Supervisor: {p.SupervisorNombre}");
                            if (!string.IsNullOrEmpty(p.SupervisorComentario))
                                c.Item().Text($"Comentario: {p.SupervisorComentario}");
                        });
                    }

                    col.Item().PaddingTop(15).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Creado por: {p.CreadoPor}");
                            c.Item().Text($"Fecha creación: {p.FechaCreacion:dd/MM/yyyy HH:mm}");
                        });
                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text($"Empresa Planta: {p.EmpresaPlanta}");
                        });
                    });
                });

                page.Footer().AlignCenter()
                    .Text(txt =>
                    {
                        txt.Span("PTS Synthon - Sistema de Permisos de Trabajo | Página ");
                        txt.CurrentPageNumber();
                        txt.Span(" de ");
                        txt.TotalPages();
                    });
            });
        });

        var pdf = doc.GeneratePdf();
        return File(pdf, "application/pdf", $"permiso_{p.NumeroPermiso}.pdf");
    }
}
