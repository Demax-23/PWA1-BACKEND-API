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

    public virtual ICollection<ActivityExecution> Executions { get; set; } = new List<ActivityExecution>();
}
