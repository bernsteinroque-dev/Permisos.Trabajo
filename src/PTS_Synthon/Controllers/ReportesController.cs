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

        var tipoLabel = p.Tipo switch { "pts" => "Permiso de Trabajo Seguro (PTS)", "caliente" => "Trabajo en Caliente", "altura" => "Trabajo en Altura", _ => p.Tipo ?? "Permiso" };
        var statusText = p.Estado switch { "approved" => "✔ APROBADO", "rejected" => "✘ RECHAZADO", _ => "⏳ PENDIENTE" };
        var statusColor = p.Estado switch { "approved" => Colors.Green.Darken2, "rejected" => Colors.Red.Darken2, _ => Colors.Orange.Darken2 };

        var verifPts = new[] { "¿Los factores externos permiten el trabajo?","¿El equipo está detenido?","¿Desconectado/bloqueado y con tarjetas?","¿Purgado/lavado/ventilado/inertizado?","¿Accesos y pisos seguros?","¿Trabajos en proximidades?","¿EPP adecuados y en buenas condiciones?","¿Zona vallada?","¿Operaciones adyacentes seguras?","¿Medidas para evitar volcamiento a desagüas?","¿Libre de sustancias peligrosas?","¿Iluminación adecuada?","¿Equipos y herramientas en buen estado?","¿Equipos de izaje adecuados?","Otras" };
        var verifs = p.Verificaciones;
        var peligros = p.Peligros;
        var epp = p.EppGeneral;

        static void PdfRow(ColumnDescriptor col, string label, string? value)
        {
            col.Item().Row(row =>
            {
                row.RelativeItem(2).Text(label).FontSize(9).FontColor(Colors.Grey.Darken2);
                row.RelativeItem(3).Text(value ?? "—").FontSize(9).Bold();
            });
            col.Item().PaddingBottom(2).LineHorizontal(0.3f).LineColor(Colors.Grey.Lighten3);
        }

        static void SectionTitle(ColumnDescriptor col, string title, string color)
        {
            col.Item().PaddingTop(10).PaddingBottom(3).BorderLeft(3).BorderColor(color)
                .PaddingLeft(6).Text(title).Bold().FontSize(10).FontColor(color);
        }

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(t => t.FontSize(9.5f).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("SYNTHON S.A.").Bold().FontSize(16).FontColor(Colors.Orange.Darken2);
                            c.Item().Text("SOP.AR01.PR.251 (6.0) — Sistema de Permisos de Trabajo").FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(170).AlignRight().Column(c =>
                        {
                            c.Item().Text($"N° {p.NumeroPermiso}").Bold().FontSize(18).FontColor(Colors.Orange.Darken2).AlignRight();
                            c.Item().Text(tipoLabel).FontSize(8).FontColor(Colors.Grey.Darken2).AlignRight();
                        });
                    });
                    col.Item().PaddingTop(4).LineHorizontal(1.5f).LineColor(Colors.Orange.Darken2);
                    col.Item().PaddingTop(4).Background(statusColor).Padding(5)
                        .Text(statusText).FontColor(Colors.White).Bold().FontSize(10).AlignCenter();
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    // DATOS GENERALES
                    SectionTitle(col, "DATOS GENERALES", Colors.Orange.Darken2);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            PdfRow(c, "N° Permiso", p.NumeroPermiso.ToString());
                            PdfRow(c, "Empresa / Planta", p.EmpresaPlanta);
                            PdfRow(c, "Proveedor / Empresa", p.ProveedorNombre ?? p.Empresa);
                            PdfRow(c, "Fecha", p.Fecha);
                            PdfRow(c, "Vigencia Desde", p.VigenciaDesde);
                            PdfRow(c, "Vigencia Hasta", p.VigenciaHasta);
                        });
                        row.ConstantItem(8);
                        row.RelativeItem().Column(c =>
                        {
                            PdfRow(c, "Orden / Pedido", p.Orden);
                            PdfRow(c, "Planta / Sector", p.Planta);
                            PdfRow(c, "Equipo / Instalación", p.Equipo);
                            PdfRow(c, "Realiza el trabajo", p.RealizaElTrabajo);
                            PdfRow(c, "Email Proveedor", p.ProveedorEmail);
                            PdfRow(c, "Creado por", p.CreadoPor);
                        });
                    });

                    // DESCRIPCIÓN
                    SectionTitle(col, "DESCRIPCIÓN DEL TRABAJO", Colors.Orange.Darken2);
                    col.Item().Background(Colors.Grey.Lighten4).Padding(6).Text(p.Descripcion ?? "—").FontSize(9);

                    // VERIFICACIONES (PTS only)
                    if (verifs != null && verifs.Count > 0 && p.Tipo == "pts")
                    {
                        SectionTitle(col, "VERIFICACIONES PREVIAS", Colors.Orange.Darken2);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(5); c.ConstantColumn(40); });
                            table.Header(h =>
                            {
                                h.Cell().Background(Colors.Orange.Lighten4).Padding(3).Text("Pregunta").Bold().FontSize(8);
                                h.Cell().Background(Colors.Orange.Lighten4).Padding(3).Text("Resp.").Bold().FontSize(8).AlignCenter();
                            });
                            for (int i = 0; i < verifPts.Length; i++)
                            {
                                var v = i < verifs.Count ? verifs[i] : "N/A";
                                var bg = v == "SI" ? Colors.Green.Lighten4 : v == "NO" ? Colors.Red.Lighten4 : Colors.Grey.Lighten4;
                                table.Cell().BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten3).Padding(3).Text($"{i + 1}. {verifPts[i]}").FontSize(8);
                                table.Cell().BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten3).Background(bg).Padding(3).Text(v).FontSize(8).Bold().AlignCenter();
                            }
                        });
                    }

                    // PELIGROS
                    if (peligros != null && peligros.Count > 0)
                    {
                        SectionTitle(col, "PELIGROS IDENTIFICADOS", Colors.Red.Darken2);
                        col.Item().Row(row => {
                            foreach (var pg in peligros)
                                row.AutoItem().Padding(2).Background(Colors.Red.Lighten4).Padding(3).Text(pg).FontSize(8);
                        });
                    }

                    // EPP
                    if (epp != null && epp.Count > 0)
                    {
                        SectionTitle(col, "EPP REQUERIDO", Colors.Orange.Darken2);
                        col.Item().Row(row => {
                            foreach (var e in epp)
                                row.AutoItem().Padding(2).Background(Colors.Orange.Lighten4).Padding(3).Text(e).FontSize(8);
                        });
                    }

                    // EQUIPOS Y CONTROLES
                    SectionTitle(col, "EQUIPOS Y CONTROLES", Colors.Orange.Darken2);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            PdfRow(c, "Herramienta Antichispa", p.HerramientaAntichispa);
                            PdfRow(c, "Iluminación Especial", p.IluminacionEspecial);
                        });
                        row.ConstantItem(8);
                        row.RelativeItem().Column(c =>
                        {
                            PdfRow(c, "Genera Residuos", p.GeneraResiduos);
                        });
                    });

                    // CONSIGNACIONES
                    SectionTitle(col, "CONSIGNACIONES", Colors.Orange.Darken2);
                    col.Item().Column(c =>
                    {
                        PdfRow(c, "¿Se consignó?", p.Consigno);
                        PdfRow(c, "¿Cuál?", p.ConsignoCual);
                        PdfRow(c, "¿Quién consignó?", p.ConsignoQuien);
                    });

                    // OBSERVACIONES
                    if (!string.IsNullOrEmpty(p.Observaciones))
                    {
                        SectionTitle(col, "OBSERVACIONES", Colors.Grey.Darken2);
                        col.Item().Background(Colors.Grey.Lighten4).Padding(6).Text(p.Observaciones).FontSize(9);
                    }

                    // FIRMAS
                    SectionTitle(col, "FIRMAS", Colors.Orange.Darken2);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            PdfRow(c, "Emisor", p.Emisor);
                            PdfRow(c, "Receptor", p.Receptor);
                        });
                        row.ConstantItem(8);
                        row.RelativeItem().Column(c =>
                        {
                            if (!string.IsNullOrEmpty(p.Ejecutante1)) PdfRow(c, "Ejecutante 1", p.Ejecutante1);
                            if (!string.IsNullOrEmpty(p.Ejecutante2)) PdfRow(c, "Ejecutante 2", p.Ejecutante2);
                            if (!string.IsNullOrEmpty(p.Ejecutante3)) PdfRow(c, "Ejecutante 3", p.Ejecutante3);
                            if (!string.IsNullOrEmpty(p.Ejecutante4)) PdfRow(c, "Ejecutante 4", p.Ejecutante4);
                        });
                    });

                    // TERMINACIÓN
                    SectionTitle(col, "TERMINACIÓN DEL TRABAJO", Colors.Green.Darken2);
                    col.Item().Column(c =>
                    {
                        PdfRow(c, "¿Terminó el trabajo?", p.TerminacionTrabajo);
                        PdfRow(c, "¿Sector limpio?", p.LimpiezaSector);
                        PdfRow(c, "Firma Terminación", p.TermFirma);
                        PdfRow(c, "Recepción Emisor", p.RecepcionEmisor);
                        PdfRow(c, "Firma Recepción", p.RecepcionFirma);
                    });

                    // SUPERVISIÓN
                    if (!string.IsNullOrEmpty(p.SupervisorNombre))
                    {
                        SectionTitle(col, "DECISIÓN DEL SUPERVISOR", Colors.Green.Darken2);
                        col.Item().Column(c =>
                        {
                            PdfRow(c, "Supervisor", p.SupervisorNombre);
                            PdfRow(c, "Estado", statusText);
                            PdfRow(c, "Comentario", p.SupervisorComentario);
                            PdfRow(c, "Fecha Aprobación", p.FechaModificacion?.ToString("dd/MM/yyyy HH:mm"));
                        });
                    }
                });

                page.Footer().AlignCenter().Text(txt =>
                {
                    txt.Span($"PTS Synthon — {tipoLabel} N° {p.NumeroPermiso} | Generado: {DateTime.Now:dd/MM/yyyy HH:mm} | Página ");
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
