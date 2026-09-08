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
}
