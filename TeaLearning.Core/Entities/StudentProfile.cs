using TeaLearning.Core.Common;

namespace TeaLearning.Core.Entities;

public class StudentProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public virtual ApplicationUser User { get; set; } = null!;

    public string CodigoEstudiante { get; set; } = string.Empty;
    public DateTime FechaNacimiento { get; set; }
    public string? ObservacionesMedicasTEA { get; set; }

    // Aula asignada
    public Guid? ClassroomId { get; set; }
    public virtual Classroom? Classroom { get; set; }

    // Acudiente responsable
    public Guid? GuardianProfileId { get; set; }
    public virtual GuardianProfile? GuardianProfile { get; set; }

    // Configuración Sensorial Adaptativa (1:1)
    public virtual SensoryPreference? SensoryPreference { get; set; }

    // Relaciones de actividades y seguimiento
    public virtual ICollection<ActivityAssignment> Assignments { get; set; } = new List<ActivityAssignment>();
    public virtual ICollection<ActivityExecution> Executions { get; set; } = new List<ActivityExecution>();
    public virtual ICollection<PedagogicalNote> PedagogicalNotes { get; set; } = new List<PedagogicalNote>();
}
