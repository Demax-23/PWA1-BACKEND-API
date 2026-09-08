using TeaLearning.Core.Common;

namespace TeaLearning.Core.Entities;

public class GuardianProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public virtual ApplicationUser User { get; set; } = null!;

    public string TelefonoContacto { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string Parentesco { get; set; } = string.Empty; // Madre, Padre, Tutor legal, etc.

    // Un Acudiente puede tener múltiples estudiantes a cargo
    public virtual ICollection<StudentProfile> Students { get; set; } = new List<StudentProfile>();
}
