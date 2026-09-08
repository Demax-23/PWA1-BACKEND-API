using TeaLearning.Core.Common;

namespace TeaLearning.Core.Entities;

public class TeacherProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public virtual ApplicationUser User { get; set; } = null!;

    public string Especialidad { get; set; } = string.Empty; // Educación Especial, Psicopedagogía, etc.
    public string? NumeroRegistroProfesional { get; set; }

    // Un Docente administra una o varias Aulas/Grupos
    public virtual ICollection<Classroom> Classrooms { get; set; } = new List<Classroom>();
    public virtual ICollection<Activity> ActivitiesCreated { get; set; } = new List<Activity>();
    public virtual ICollection<PedagogicalNote> PedagogicalNotes { get; set; } = new List<PedagogicalNote>();
}
