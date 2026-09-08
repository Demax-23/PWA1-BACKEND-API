using TeaLearning.Core.Common;

namespace TeaLearning.Core.Entities;

public class Classroom : BaseEntity
{
    public string Nombre { get; set; } = string.Empty; // Ej. "Aula Azul - Nivel Inicial"
    public string Grado { get; set; } = string.Empty;
    public int AnioLectivo { get; set; } = DateTime.UtcNow.Year;

    public Guid TeacherProfileId { get; set; }
    public virtual TeacherProfile TeacherProfile { get; set; } = null!;

    // Estudiantes que pertenecen al aula
    public virtual ICollection<StudentProfile> Students { get; set; } = new List<StudentProfile>();
}
