using TeaLearning.Core.Common;

namespace TeaLearning.Core.Entities;

public class ActivityExecution : BaseEntity
{
    public Guid AssignmentId { get; set; }
    public virtual ActivityAssignment Assignment { get; set; } = null!;

    public Guid StudentProfileId { get; set; }
    public virtual StudentProfile StudentProfile { get; set; } = null!;

    public int TiempoRespuestaSegundos { get; set; }
    public int CantidadIntentosFallidos { get; set; }
    public int CantidadAciertos { get; set; }
    public bool CompletadaExitosamente { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}
