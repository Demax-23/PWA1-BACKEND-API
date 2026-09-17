using TeaLearning.Core.Enums;

namespace TeaLearning.Services.DTOs.Assignments;

public class AssignmentDto
{
    public Guid Id { get; set; }
    public Guid ActivityId { get; set; }
    public string ActivityTitulo { get; set; } = string.Empty;
    public string TipoActividad { get; set; } = string.Empty;
    public Guid StudentProfileId { get; set; }
    public string StudentNombre { get; set; } = string.Empty;
    public string CodigoEstudiante { get; set; } = string.Empty;
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaLimite { get; set; }
    public int OrdenSecuencial { get; set; }
    public PrioridadActividad Prioridad { get; set; }
    public string PrioridadNombre => Prioridad.ToString();
    public EstadoAsignacion Estado { get; set; }
    public string EstadoNombre => Estado.ToString();
    public DateTime CreatedAt { get; set; }
}
