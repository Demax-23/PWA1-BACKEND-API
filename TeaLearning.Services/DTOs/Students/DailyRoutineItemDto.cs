namespace TeaLearning.Services.DTOs.Students;

public class DailyRoutineItemDto
{
    public Guid AssignmentId { get; set; }
    public Guid ActivityId { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string TipoActividad { get; set; } = string.Empty;
    public string ObjetivoPedagogico { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = "{}";
    public string ModuloTitulo { get; set; } = string.Empty;
    public int OrdenSecuencial { get; set; }
    public string Prioridad { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty; // Pendiente, EnProgreso, Completada
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaLimite { get; set; }
}
