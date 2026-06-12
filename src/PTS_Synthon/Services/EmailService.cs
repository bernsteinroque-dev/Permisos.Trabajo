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

    public async Task SendNuevoPermisoAsync(Permiso permiso, string toEmail)
    {
        if (string.IsNullOrEmpty(toEmail)) return;

        var html = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;'>
  <div style='background:#E87722;color:white;padding:20px;'>
    <h2 style='margin:0;'>Nuevo Permiso de Trabajo</h2>
    <p style='margin:5px 0 0;'>PTS Synthon - Sistema de Permisos</p>
  </div>
  <div style='padding:20px;background:#f9f9f9;'>
    <p>Se ha creado un nuevo permiso de trabajo que requiere aprobación.</p>
    <table style='width:100%;border-collapse:collapse;'>
      <tr><td style='padding:8px;border-bottom:1px solid #ddd;font-weight:bold;width:40%;'>N° Permiso:</td><td style='padding:8px;border-bottom:1px solid #ddd;'>{permiso.NumeroPermiso}</td></tr>
      <tr><td style='padding:8px;border-bottom:1px solid #ddd;font-weight:bold;'>Tipo:</td><td style='padding:8px;border-bottom:1px solid #ddd;'>{permiso.Tipo?.ToUpper()}</td></tr>
      <tr><td style='padding:8px;border-bottom:1px solid #ddd;font-weight:bold;'>Proveedor:</td><td style='padding:8px;border-bottom:1px solid #ddd;'>{permiso.ProveedorNombre ?? "-"}</td></tr>
      <tr><td style='padding:8px;border-bottom:1px solid #ddd;font-weight:bold;'>Descripción:</td><td style='padding:8px;border-bottom:1px solid #ddd;'>{permiso.Descripcion ?? "-"}</td></tr>
      <tr><td style='padding:8px;border-bottom:1px solid #ddd;font-weight:bold;'>Creado por:</td><td style='padding:8px;border-bottom:1px solid #ddd;'>{permiso.CreadoPor}</td></tr>
      <tr><td style='padding:8px;font-weight:bold;'>Fecha:</td><td style='padding:8px;'>{permiso.FechaCreacion:dd/MM/yyyy HH:mm}</td></tr>
    </table>
    <p style='margin-top:20px;'>Acceda al sistema PTS Synthon para aprobar o rechazar este permiso.</p>
  </div>
  <div style='background:#4A4A4A;color:white;padding:10px 20px;font-size:12px;'>
    <p style='margin:0;'>PTS Synthon - Synthon Argentina S.A.</p>
  </div>
</div>";

        var msg = CreateMessage(toEmail, $"[PTS] Nuevo Permiso #{permiso.NumeroPermiso} - {permiso.ProveedorNombre}", html);
        using var client = await CreateClientAsync();
        await client.SendAsync(msg);
        await client.DisconnectAsync(true);
        _logger.LogInformation("Email enviado para permiso {No}", permiso.NumeroPermiso);
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
