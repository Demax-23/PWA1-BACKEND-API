using TeaLearning.Core.Common;
using TeaLearning.Core.Enums;

namespace TeaLearning.Core.Entities;

public class ActivityAssignment : BaseEntity
{
    public Guid ActivityId { get; set; }
    public virtual Activity Activity { get; set; } = null!;

    public Guid StudentProfileId { get; set; }
    public virtual StudentProfile StudentProfile { get; set; } = null!;

    public DateTime FechaInicio { get; set; } = DateTime.UtcNow;
    public DateTime? FechaLimite { get; set; }

    // Orden en la rutina visual diaria del estudiante
    public int OrdenSecuencial { get; set; } = 1;

    public PrioridadActividad Prioridad { get; set; } = PrioridadActividad.Media;
    public EstadoAsignacion Estado { get; set; } = EstadoAsignacion.Pendiente;

    // Calificación pedagógica (escala 1.0 - 5.0) y retroalimentación docente
    public decimal? Calificacion { get; set; }
    public string? ComentarioCalificacion { get; set; }
    public DateTime? FechaCalificacion { get; set; }
    public Guid? CalificadoPorDocenteId { get; set; }
    public virtual TeacherProfile? CalificadoPorDocente { get; set; }

    public virtual ICollection<ActivityExecution> Executions { get; set; } = new List<ActivityExecution>();
}
