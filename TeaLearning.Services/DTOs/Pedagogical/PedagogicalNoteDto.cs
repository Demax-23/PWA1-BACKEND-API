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
    public string Titulo => !string.IsNullOrEmpty(Contenido) && Contenido.Length > 40 ? Contenido.Substring(0, 40) + "..." : (Contenido ?? "Observación");
    public string Tipo => "Observacion";
    public string Area => "Socioemocional";
    public bool VisibleAcudiente => true;
    public bool PuedeEditar => true;
}
