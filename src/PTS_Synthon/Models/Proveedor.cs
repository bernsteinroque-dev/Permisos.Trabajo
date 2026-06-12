using System.Text.Json.Serialization;

namespace PTS_Synthon.Models;

public class Proveedor
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("no")]
    public int NumeroProveedor { get; set; }

    [JsonPropertyName("razon")]
    public string RazonSocial { get; set; } = string.Empty;

    [JsonPropertyName("cuit")]
    public string? CUIT { get; set; }

    [JsonPropertyName("rubro")]
    public string? Rubro { get; set; }

    [JsonPropertyName("estado")]
    public string? Estado { get; set; } = "Activo";

    [JsonPropertyName("habil")]
    public string? Habilitacion { get; set; }

    [JsonPropertyName("contacto")]
    public string? Contacto { get; set; }

    [JsonPropertyName("tel")]
    public string? Telefono { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("cargo")]
    public string? Cargo { get; set; }

    [JsonPropertyName("art")]
    public string? VencART { get; set; }

    [JsonPropertyName("rc")]
    public string? VencRC { get; set; }

    [JsonPropertyName("hab_venc")]
    public string? VencHabMunicipal { get; set; }

    [JsonPropertyName("afip")]
    public string? VencAFIP { get; set; }

    [JsonPropertyName("sih")]
    public string? VencSegHigiene { get; set; }

    [JsonPropertyName("otros")]
    public string? VencOtros { get; set; }

    [JsonPropertyName("obs_doc")]
    public string? ObsDocumentacion { get; set; }

    [JsonPropertyName("obs")]
    public string? Observaciones { get; set; }

    [JsonPropertyName("fechaCreacion")]
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("creadoPor")]
    public string CreadoPor { get; set; } = string.Empty;

    [JsonIgnore]
    public ICollection<Permiso> Permisos { get; set; } = new List<Permiso>();
}
