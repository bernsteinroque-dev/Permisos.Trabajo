using System.Text.Json;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;

namespace PTS_Synthon.Models;

public class Permiso
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("no")]
    public int NumeroPermiso { get; set; }

    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Estado { get; set; } = "pending";

    [JsonPropertyName("fechaCreacion")]
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("fechaModificacion")]
    public DateTime? FechaModificacion { get; set; }

    // DB column for CreadoPor, exposed to JSON as "usuario_creador"
    [JsonPropertyName("usuario_creador")]
    public string CreadoPor { get; set; } = string.Empty;

    [JsonPropertyName("modificadoPor")]
    public string? ModificadoPor { get; set; }

    [JsonPropertyName("supNombre")]
    public string? SupervisorNombre { get; set; }

    [JsonPropertyName("supComentario")]
    public string? SupervisorComentario { get; set; }

    [JsonPropertyName("empresa_planta")]
    public string EmpresaPlanta { get; set; } = "Synthon S.A.";

    [JsonPropertyName("provId")]
    public int? ProveedorId { get; set; }

    [JsonIgnore]
    public Proveedor? Proveedor { get; set; }

    [JsonPropertyName("provNombre")]
    public string? ProveedorNombre { get; set; }

    [JsonPropertyName("provEmail")]
    public string? ProveedorEmail { get; set; }

    [JsonPropertyName("empresa")]
    public string? Empresa { get; set; }

    [JsonPropertyName("fecha")]
    public string? Fecha { get; set; }

    [JsonPropertyName("desde")]
    public string? VigenciaDesde { get; set; }

    [JsonPropertyName("hasta")]
    public string? VigenciaHasta { get; set; }

    [JsonPropertyName("orden")]
    public string? Orden { get; set; }

    [JsonPropertyName("planta")]
    public string? Planta { get; set; }

    [JsonPropertyName("equipo")]
    public string? Equipo { get; set; }

    [JsonPropertyName("desc")]
    public string? Descripcion { get; set; }

    [JsonPropertyName("realiza")]
    public string? RealizaElTrabajo { get; set; }

    // --- JSON stored columns (DB) + computed properties (API) ---

    [JsonIgnore]
    public string? VerificacionesJson { get; set; }

    [NotMapped]
    [JsonPropertyName("verifs")]
    public List<string>? Verificaciones
    {
        get => string.IsNullOrEmpty(VerificacionesJson) ? null
             : JsonSerializer.Deserialize<List<string>>(VerificacionesJson);
        set => VerificacionesJson = value == null ? null : JsonSerializer.Serialize(value);
    }

    [JsonIgnore]
    public string? PeligrosJson { get; set; }

    [NotMapped]
    [JsonPropertyName("peligros")]
    public List<string>? Peligros
    {
        get => string.IsNullOrEmpty(PeligrosJson) ? null
             : JsonSerializer.Deserialize<List<string>>(PeligrosJson);
        set => PeligrosJson = value == null ? null : JsonSerializer.Serialize(value);
    }

    [JsonIgnore]
    public string? EppGeneralJson { get; set; }

    [NotMapped]
    [JsonPropertyName("epp")]
    public List<string>? EppGeneral
    {
        get => string.IsNullOrEmpty(EppGeneralJson) ? null
             : JsonSerializer.Deserialize<List<string>>(EppGeneralJson);
        set => EppGeneralJson = value == null ? null : JsonSerializer.Serialize(value);
    }

    [JsonIgnore]
    public string? CheckListJson { get; set; }

    [NotMapped]
    [JsonPropertyName("cl")]
    public object? CheckList
    {
        get => string.IsNullOrEmpty(CheckListJson) ? null
             : JsonSerializer.Deserialize<object>(CheckListJson);
        set => CheckListJson = value == null ? null : JsonSerializer.Serialize(value);
    }

    // --- Controles ---

    [JsonPropertyName("anti")]
    public string? HerramientaAntichispa { get; set; }

    [JsonPropertyName("ilum")]
    public string? IluminacionEspecial { get; set; }

    [JsonPropertyName("resi")]
    public string? GeneraResiduos { get; set; }

    // --- Consignaciones ---

    [JsonPropertyName("cons")]
    public string? Consigno { get; set; }

    [JsonPropertyName("cons_c")]
    public string? ConsignoCual { get; set; }

    [JsonPropertyName("cons_q")]
    public string? ConsignoQuien { get; set; }

    // --- Observaciones ---

    [JsonPropertyName("obs")]
    public string? Observaciones { get; set; }

    // --- Firmas ---

    [JsonPropertyName("emisor")]
    public string? Emisor { get; set; }

    [JsonPropertyName("receptor")]
    public string? Receptor { get; set; }

    [JsonPropertyName("ej1")]
    public string? Ejecutante1 { get; set; }

    [JsonPropertyName("ej2")]
    public string? Ejecutante2 { get; set; }

    [JsonPropertyName("ej3")]
    public string? Ejecutante3 { get; set; }

    [JsonPropertyName("ej4")]
    public string? Ejecutante4 { get; set; }

    // --- Terminacion ---

    [JsonPropertyName("term_trabajo")]
    public string? TerminacionTrabajo { get; set; }

    [JsonPropertyName("term_limpio")]
    public string? LimpiezaSector { get; set; }

    [JsonPropertyName("term_firma")]
    public string? TermFirma { get; set; }

    [JsonPropertyName("recep_emisor")]
    public string? RecepcionEmisor { get; set; }

    [JsonPropertyName("recep_firma")]
    public string? RecepcionFirma { get; set; }

    [JsonPropertyName("emailNotificado")]
    public bool EmailNotificado { get; set; }
}
