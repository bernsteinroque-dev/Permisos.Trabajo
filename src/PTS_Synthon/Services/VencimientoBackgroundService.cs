using Microsoft.EntityFrameworkCore;
using PTS_Synthon.Data;

namespace PTS_Synthon.Services;

public class VencimientoBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<VencimientoBackgroundService> _logger;

    public VencimientoBackgroundService(
        IServiceProvider services,
        IConfiguration config,
        ILogger<VencimientoBackgroundService> logger)
    {
        _services = services;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextRun = DateTime.Today.AddDays(1).AddHours(7);
            if (now.Hour < 7)
                nextRun = DateTime.Today.AddHours(7);

            var delay = nextRun - now;
            if (delay.TotalMilliseconds > 0)
                await Task.Delay(delay, stoppingToken);

            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                await CheckVencimientosAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking vencimientos");
            }
        }
    }

    private async Task CheckVencimientosAsync()
    {
        var supervisorEmail = _config.GetValue<string>("NotificacionSettings:SupervisorEmail") ?? "";
        var daysAlert = _config.GetValue<int>("NotificacionSettings:DaysBeforeVencimientoAlert");
        if (daysAlert == 0) daysAlert = 30;

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailSvc = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var hoy = DateTime.Today;
        var limite = hoy.AddDays(daysAlert);

        var proveedores = await db.Proveedores.ToListAsync();
        var alertas = new List<(string Proveedor, string Documento, int Dias)>();

        foreach (var p in proveedores)
        {
            AddAlerta(p.VencART, "ART", p.RazonSocial, hoy, limite, alertas);
            AddAlerta(p.VencRC, "Resp. Civil", p.RazonSocial, hoy, limite, alertas);
            AddAlerta(p.VencHabMunicipal, "Hab. Municipal", p.RazonSocial, hoy, limite, alertas);
            AddAlerta(p.VencAFIP, "AFIP", p.RazonSocial, hoy, limite, alertas);
            AddAlerta(p.VencSegHigiene, "Seg. e Higiene", p.RazonSocial, hoy, limite, alertas);
            AddAlerta(p.VencOtros, "Otros", p.RazonSocial, hoy, limite, alertas);
        }

        if (alertas.Any() && !string.IsNullOrEmpty(supervisorEmail))
        {
            await emailSvc.SendVencimientoConsolidadoAsync(supervisorEmail, alertas);
            _logger.LogInformation("Sent vencimiento alert with {Count} items", alertas.Count);
        }
    }

    private static void AddAlerta(string? fechaStr, string doc, string proveedor,
        DateTime hoy, DateTime limite, List<(string, string, int)> list)
    {
        if (!string.IsNullOrEmpty(fechaStr) && DateTime.TryParse(fechaStr, out var fecha))
        {
            if (fecha >= hoy && fecha <= limite)
                list.Add((proveedor, doc, (int)(fecha - hoy).TotalDays));
        }
    }
}
