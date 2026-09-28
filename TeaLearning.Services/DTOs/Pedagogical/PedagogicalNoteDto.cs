namespace TeaLearning.Services.DTOs.Pedagogical;

public class PedagogicalNoteDto
{
    public Guid Id { get; set; }
    public Guid StudentProfileId { get; set; }
    public string StudentNombre { get; set; } = string.Empty;
    public Guid TeacherProfileId { get; set; }
    public string TeacherNombre { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
}
