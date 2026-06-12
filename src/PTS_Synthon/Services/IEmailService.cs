using PTS_Synthon.Models;

namespace PTS_Synthon.Services;

public interface IEmailService
{
    Task SendNuevoPermisoAsync(Permiso permiso, string toEmail);
    Task SendPermisoAprobadoAsync(Permiso permiso);
    Task SendPermisoRechazadoAsync(Permiso permiso);
    Task SendVencimientoAlertAsync(string toEmail, string proveedorNombre, string documento, int diasRestantes);
    Task SendVencimientoConsolidadoAsync(string toEmail, List<(string Proveedor, string Documento, int Dias)> alertas);
}
