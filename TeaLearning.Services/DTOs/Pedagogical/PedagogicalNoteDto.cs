namespace TeaLearning.Services.DTOs.Pedagogical;

public class PedagogicalNoteDto
{
    public Guid Id { get; set; }
    public Guid StudentProfileId { get; set; }
    public string StudentNombre { get; set; } = string.Empty;
    public Guid TeacherProfileId { get; set; }
    public string TeacherNombre { get; set; } = string.Empty;
    public string DocenteNombre => TeacherNombre;
    public string Contenido { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
    public string Fecha => FechaRegistro.ToString("yyyy-MM-dd");

    public string Titulo { get; set; } = string.Empty;
    public string Tipo { get; set; } = "Observacion";
    public string Area { get; set; } = "Socioemocional";
    public string? RecomendacionCasa { get; set; }
    public bool VisibleAcudiente { get; set; } = true;
    public Guid? AssignmentId { get; set; }
    public bool PuedeEditar { get; set; } = true;
}
