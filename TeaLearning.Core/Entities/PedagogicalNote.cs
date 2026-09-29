using TeaLearning.Core.Common;

namespace TeaLearning.Core.Entities;

public class PedagogicalNote : BaseEntity
{
    public Guid StudentProfileId { get; set; }
    public virtual StudentProfile StudentProfile { get; set; } = null!;

    public Guid TeacherProfileId { get; set; }
    public virtual TeacherProfile TeacherProfile { get; set; } = null!;

    public string Contenido { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public string Titulo { get; set; } = string.Empty;
    public string Tipo { get; set; } = "Observacion";
    public string Area { get; set; } = "Socioemocional";
    public string? RecomendacionCasa { get; set; }
    public bool VisibleAcudiente { get; set; } = true;
    public Guid? AssignmentId { get; set; }
}
