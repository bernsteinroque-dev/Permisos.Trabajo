using System.Text.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using PTS_Synthon.Models;

namespace PTS_Synthon.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    private async Task<SmtpClient> CreateClientAsync()
    {
        var client = new SmtpClient();
        var server = _config.GetValue<string>("EmailSettings:SmtpServer") ?? "smtp.office365.com";
        var port = _config.GetValue<int>("EmailSettings:SmtpPort");
        var useTls = _config.GetValue<bool>("EmailSettings:UseTLS");

        await client.ConnectAsync(server, port, useTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None);

        var user = _config.GetValue<string>("EmailSettings:SenderEmail") ?? "";
        var pass = _config.GetValue<string>("EmailSettings:SenderPassword") ?? "";
        if (!string.IsNullOrEmpty(pass) && pass != "REPLACE_WITH_APP_PASSWORD")
            await client.AuthenticateAsync(user, pass);

        return client;
    }

    private MimeMessage CreateMessage(string to, string subject, string htmlBody)
    {
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(
            _config.GetValue<string>("EmailSettings:SenderName") ?? "PTS Synthon",
            _config.GetValue<string>("EmailSettings:SenderEmail") ?? "pts@synthon.com.ar"));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = htmlBody };
        msg.Body = builder.ToMessageBody();
        return msg;
    }

    private static string Row(string key, string? value) =>
        $"<tr><td style='padding:7px 10px;font-weight:600;color:#5a4a3a;background:#faf7f3;width:38%;border-bottom:1px solid #ede8e0;'>{key}</td><td style='padding:7px 10px;color:#3a2e1e;border-bottom:1px solid #ede8e0;'>{value ?? "—"}</td></tr>";

    private static string Section(string title, string color, string rows) =>
        $"<tr><td colspan='2' style='padding:14px 10px 4px;font-weight:700;font-size:13px;color:{color};border-top:3px solid {color};background:#fff;letter-spacing:.5px;text-transform:uppercase'>{title}</td></tr>{rows}";

    private static readonly string[] VerifPts = [
        "¿Los factores externos permiten que el trabajo se haga con seguridad?",
        "¿El equipo está detenido?",
        "¿Se ha desconectado y bloqueado eléctrica y/o mecánicamente?",
        "¿El equipo/cañería/tanque ha sido debidamente purgado, lavado, ventilado?",
        "¿Los lugares de acceso y los pisos permiten realizar la tarea en forma segura?",
        "¿Existen otros trabajos en las proximidades?",
        "¿Los elementos de protección personal son adecuados?",
        "¿Se ha vallado la zona de trabajo?",
        "¿Las operaciones y equipos adyacentes permiten realizar el trabajo?",
        "¿Se tomaron medidas para evitar volcamiento a desagüas?",
        "¿El lugar está libre de sustancias químicas peligrosas?",
        "¿La iluminación del sector permite realizar el trabajo en forma segura?",
        "¿Los equipos y herramientas se encuentran en buen estado?",
        "¿El o los equipos de izaje son los adecuados?",
        "Otras."
    ];

    private static readonly string[] VerifCal = [
        "¿Las máquinas y/o herramientas se encuentran en buenas condiciones?",
        "¿El lugar ha sido ventilado y se han abierto las bocas de inspección?",
        "¿El sector/equipo/cañería ha sido lavado y está libre de inflamables?",
        "¿Si la cañería contuvo ácido/gas/inflamables, se midió explosividad?",
        "¿Las proximidades del trabajo están libres de elementos combustibles?",
        "¿Se cuenta con mantas ignífugas para proteger contra chispas?",
        "¿Se protegieron desagüas/cañerías para impedir emanación gaseosa?",
        "¿Se cuenta con un extintor en el lugar de trabajo?"
    ];

    public async Task SendNuevoPermisoAsync(Permiso permiso, string toEmail)
    {
        if (string.IsNullOrEmpty(toEmail)) return;

        var tipoLabel = permiso.Tipo switch {
            "pts"      => "Permiso de Trabajo Seguro",
            "caliente" => "Trabajo en Caliente",
            "altura"   => "Trabajo en Altura",
            _          => permiso.Tipo?.ToUpper() ?? "—"
        };
        var tipoColor = permiso.Tipo switch {
            "caliente" => "#C07028",
            "altura"   => "#2980b9",
            _          => "#B08D57"
        };
        var estadoLabel = permiso.Estado switch {
            "approved" => "✅ APROBADO",
            "rejected" => "❌ RECHAZADO",
            _          => "⏳ PENDIENTE DE APROBACIÓN"
        };

        // Datos generales
        var rows = Section("Datos Generales", tipoColor,
            Row("N° Permiso", permiso.NumeroPermiso.ToString()) +
            Row("Tipo", tipoLabel) +
            Row("Estado", estadoLabel) +
            Row("Empresa / Planta", permiso.EmpresaPlanta) +
            Row("Proveedor / Empresa", permiso.ProveedorNombre) +
            Row("Email Proveedor", permiso.ProveedorEmail) +
            Row("Fecha", permiso.Fecha) +
            Row("Vigencia Desde", permiso.VigenciaDesde) +
            Row("Vigencia Hasta", permiso.VigenciaHasta) +
            Row("Orden / Pedido N°", permiso.Orden) +
            Row("Planta / Sector", permiso.Planta) +
            Row("Equipo / Instalación", permiso.Equipo) +
            Row("Realiza el Trabajo", permiso.RealizaElTrabajo) +
            Row("Descripción del Trabajo", permiso.Descripcion) +
            Row("Creado por", permiso.CreadoPor) +
            Row("Fecha Sistema", permiso.FechaCreacion.ToLocalTime().ToString("dd/MM/yyyy HH:mm")));

        // Verificaciones
        var verifs = permiso.Verificaciones;
        if (verifs != null && verifs.Count > 0)
        {
            var verifQuestions = permiso.Tipo == "caliente" ? VerifCal : VerifPts;
            var verifRows = "";
            for (int i = 0; i < Math.Min(verifs.Count, verifQuestions.Length); i++)
            {
                var color = verifs[i] == "SI" ? "#27ae60" : verifs[i] == "NO" ? "#e74c3c" : "#888";
                verifRows += $"<tr><td style='padding:6px 10px;font-size:12px;color:#5a4a3a;border-bottom:1px solid #ede8e0;background:#faf7f3;'>{i+1}. {verifQuestions[i]}</td><td style='padding:6px 10px;font-weight:700;color:{color};border-bottom:1px solid #ede8e0;'>{verifs[i]}</td></tr>";
            }
            rows += Section("Verificaciones Previas", tipoColor, verifRows);
        }

        // Peligros
        var peligros = permiso.Peligros;
        if (peligros != null && peligros.Count > 0)
            rows += Section("Peligros Identificados", "#c0392b",
                $"<tr><td colspan='2' style='padding:8px 10px;color:#3a2e1e;border-bottom:1px solid #ede8e0;'>{string.Join(" · ", peligros)}</td></tr>");

        // EPP
        var epp = permiso.EppGeneral;
        if (epp != null && epp.Count > 0)
            rows += Section("EPP Adicional Requerido", "#d35400",
                $"<tr><td colspan='2' style='padding:8px 10px;color:#3a2e1e;border-bottom:1px solid #ede8e0;'>{string.Join(" · ", epp)}</td></tr>");

        // Equipos / Controles (solo PTS)
        if (permiso.Tipo == "pts")
        {
            rows += Section("Equipos / Controles", tipoColor,
                Row("Herramienta antichispa", permiso.HerramientaAntichispa) +
                Row("Iluminación especial", permiso.IluminacionEspecial) +
                Row("Genera residuos", permiso.GeneraResiduos));

            rows += Section("Consignaciones", "#8e6b3e",
                Row("¿Se consignó?", permiso.Consigno) +
                Row("¿Cuál?", permiso.ConsignoCual) +
                Row("¿Quién consignó?", permiso.ConsignoQuien));
        }

        // Observaciones
        if (!string.IsNullOrEmpty(permiso.Observaciones))
            rows += Section("Observaciones", "#555",
                $"<tr><td colspan='2' style='padding:8px 10px;color:#3a2e1e;border-bottom:1px solid #ede8e0;font-style:italic;'>{permiso.Observaciones}</td></tr>");

        // Firmas
        var ejs = new[] { permiso.Ejecutante1, permiso.Ejecutante2, permiso.Ejecutante3, permiso.Ejecutante4 }.Where(e => !string.IsNullOrEmpty(e));
        rows += Section("Firmas y Ejecutantes", tipoColor,
            Row("Emisor", permiso.Emisor) +
            Row("Receptor", permiso.Receptor) +
            (ejs.Any() ? Row("Ejecutantes", string.Join(" / ", ejs)) : ""));

        // Terminación
        if (!string.IsNullOrEmpty(permiso.TerminacionTrabajo) || !string.IsNullOrEmpty(permiso.LimpiezaSector))
            rows += Section("Terminación del Trabajo", "#27ae60",
                Row("¿Terminó el trabajo?", permiso.TerminacionTrabajo) +
                Row("¿Sector limpio?", permiso.LimpiezaSector) +
                Row("Firma Terminación", permiso.TermFirma) +
                Row("Recepción Emisor", permiso.RecepcionEmisor));

        // Supervisor
        if (!string.IsNullOrEmpty(permiso.SupervisorNombre))
            rows += Section("Decisión del Supervisor", permiso.Estado == "approved" ? "#27ae60" : "#e74c3c",
                Row("Supervisor", permiso.SupervisorNombre) +
                Row("Comentario", permiso.SupervisorComentario));

        var html = $@"<!DOCTYPE html><html><body style='margin:0;padding:0;background:#f0ece6;font-family:Arial,sans-serif;'>
<div style='max-width:700px;margin:20px auto;background:#fff;border-radius:10px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,.12);'>
  <div style='background:{tipoColor};color:white;padding:22px 28px;'>
    <div style='font-size:11px;letter-spacing:2px;text-transform:uppercase;opacity:.8;margin-bottom:6px;'>Synthon Argentina S.A. · Sistema PTS Digital</div>
    <div style='font-size:22px;font-weight:700;margin-bottom:4px;'>{tipoLabel}</div>
    <div style='font-size:28px;font-weight:800;letter-spacing:-1px;'>N° {permiso.NumeroPermiso}</div>
    <div style='margin-top:10px;background:rgba(255,255,255,.2);display:inline-block;padding:4px 12px;border-radius:20px;font-size:13px;font-weight:700;'>{estadoLabel}</div>
  </div>
  <div style='padding:0 20px 20px;'>
    <table style='width:100%;border-collapse:collapse;font-size:13px;'>
      {rows}
    </table>
  </div>
  <div style='background:#4a4a4a;color:rgba(255,255,255,.7);padding:12px 20px;font-size:11px;'>
    Generado por PTS Synthon · Sistema Digital de Permisos de Trabajo · Synthon Argentina S.A. · {DateTime.Now:dd/MM/yyyy HH:mm}
  </div>
</div>
</body></html>";

        var msg = CreateMessage(toEmail, $"[PTS #{permiso.NumeroPermiso}] {tipoLabel} — {permiso.ProveedorNombre ?? permiso.CreadoPor}", html);
        using var client = await CreateClientAsync();
        await client.SendAsync(msg);
        await client.DisconnectAsync(true);
        _logger.LogInformation("Email completo enviado para permiso {No} a {Email}", permiso.NumeroPermiso, toEmail);
    }

    public async Task SendPermisoAprobadoAsync(Permiso permiso)
    {
        if (string.IsNullOrEmpty(permiso.ProveedorEmail)) return;

        var html = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;'>
  <div style='background:#22A06B;color:white;padding:20px;'>
    <h2 style='margin:0;'>Permiso Aprobado</h2>
    <p style='margin:5px 0 0;'>PTS Synthon - Sistema de Permisos</p>
  </div>
  <div style='padding:20px;'>
    <p>Su permiso de trabajo ha sido <strong>APROBADO</strong>.</p>
    <table style='width:100%;border-collapse:collapse;'>
      <tr><td style='padding:8px;border-bottom:1px solid #ddd;font-weight:bold;width:40%;'>N° Permiso:</td><td style='padding:8px;border-bottom:1px solid #ddd;'>{permiso.NumeroPermiso}</td></tr>
      <tr><td style='padding:8px;border-bottom:1px solid #ddd;font-weight:bold;'>Supervisor:</td><td style='padding:8px;border-bottom:1px solid #ddd;'>{permiso.SupervisorNombre ?? "-"}</td></tr>
      <tr><td style='padding:8px;font-weight:bold;'>Comentario:</td><td style='padding:8px;'>{permiso.SupervisorComentario ?? "-"}</td></tr>
    </table>
  </div>
</div>";

        var msg = CreateMessage(permiso.ProveedorEmail, $"[PTS] Permiso #{permiso.NumeroPermiso} APROBADO", html);
        using var client = await CreateClientAsync();
        await client.SendAsync(msg);
        await client.DisconnectAsync(true);
    }

    public async Task SendPermisoRechazadoAsync(Permiso permiso)
    {
        if (string.IsNullOrEmpty(permiso.ProveedorEmail)) return;

        var html = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;'>
  <div style='background:#E03E3E;color:white;padding:20px;'>
    <h2 style='margin:0;'>Permiso Rechazado</h2>
    <p style='margin:5px 0 0;'>PTS Synthon - Sistema de Permisos</p>
  </div>
  <div style='padding:20px;'>
    <p>Su permiso de trabajo ha sido <strong>RECHAZADO</strong>.</p>
    <table style='width:100%;border-collapse:collapse;'>
      <tr><td style='padding:8px;border-bottom:1px solid #ddd;font-weight:bold;width:40%;'>N° Permiso:</td><td style='padding:8px;border-bottom:1px solid #ddd;'>{permiso.NumeroPermiso}</td></tr>
      <tr><td style='padding:8px;border-bottom:1px solid #ddd;font-weight:bold;'>Supervisor:</td><td style='padding:8px;border-bottom:1px solid #ddd;'>{permiso.SupervisorNombre ?? "-"}</td></tr>
      <tr><td style='padding:8px;font-weight:bold;'>Motivo:</td><td style='padding:8px;'>{permiso.SupervisorComentario ?? "-"}</td></tr>
    </table>
    <p>Comuníquese con el supervisor para más información.</p>
  </div>
</div>";

        var msg = CreateMessage(permiso.ProveedorEmail, $"[PTS] Permiso #{permiso.NumeroPermiso} RECHAZADO", html);
        using var client = await CreateClientAsync();
        await client.SendAsync(msg);
        await client.DisconnectAsync(true);
    }

    public async Task SendVencimientoAlertAsync(string toEmail, string proveedorNombre, string documento, int diasRestantes)
    {
        var alertas = new List<(string, string, int)> { (proveedorNombre, documento, diasRestantes) };
        await SendVencimientoConsolidadoAsync(toEmail, alertas);
    }

    public async Task SendVencimientoConsolidadoAsync(string toEmail, List<(string Proveedor, string Documento, int Dias)> alertas)
    {
        if (string.IsNullOrEmpty(toEmail) || !alertas.Any()) return;

        var rows = string.Join("", alertas.Select(a =>
            $"<tr><td style='padding:8px;border-bottom:1px solid #ddd;'>{a.Proveedor}</td><td style='padding:8px;border-bottom:1px solid #ddd;'>{a.Documento}</td><td style='padding:8px;border-bottom:1px solid #ddd;color:{(a.Dias <= 7 ? "red" : "orange")};font-weight:bold;'>{a.Dias} días</td></tr>"));

        var html = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;'>
  <div style='background:#E87722;color:white;padding:20px;'>
    <h2 style='margin:0;'>Alerta de Vencimientos</h2>
    <p style='margin:5px 0 0;'>PTS Synthon - Sistema de Permisos</p>
  </div>
  <div style='padding:20px;'>
    <p>Los siguientes documentos de proveedores vencen en los próximos 30 días:</p>
    <table style='width:100%;border-collapse:collapse;'>
      <thead><tr style='background:#f0f0f0;'>
        <th style='padding:8px;text-align:left;'>Proveedor</th>
        <th style='padding:8px;text-align:left;'>Documento</th>
        <th style='padding:8px;text-align:left;'>Días Restantes</th>
      </tr></thead>
      <tbody>{rows}</tbody>
    </table>
  </div>
</div>";

        var msg = CreateMessage(toEmail, $"[PTS] Alerta: {alertas.Count} documentos por vencer", html);
        using var client = await CreateClientAsync();
        await client.SendAsync(msg);
        await client.DisconnectAsync(true);
    }
}
