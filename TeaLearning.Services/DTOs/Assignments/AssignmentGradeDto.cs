namespace TeaLearning.Services.DTOs.Assignments;

public class AssignmentGradeDto
{
    public Guid AssignmentId { get; set; }
    public Guid ActivityId { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string TipoActividad { get; set; } = string.Empty;
    public decimal? Calificacion { get; set; }
    public string? Comentario { get; set; }
    public DateTime? FechaCalificacion { get; set; }
    public string? DocenteNombre { get; set; }
    public bool PuedeEditar { get; set; } = true;
    public ActivityLastAttemptDto? UltimoIntento { get; set; }
    public int TotalIntentos { get; set; }
}

public class ActivityLastAttemptDto
{
    public DateTime FechaRegistro { get; set; }
    public int CantidadAciertos { get; set; }
    public int CantidadIntentosFallidos { get; set; }
    public bool CompletadaExitosamente { get; set; }
    public int TiempoRespuestaSegundos { get; set; }
}
