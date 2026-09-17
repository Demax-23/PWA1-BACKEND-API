namespace TeaLearning.Services.DTOs.Execution;

public class ExecutionResultDto
{
    public Guid ExecutionId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid StudentProfileId { get; set; }
    public int TiempoRespuestaSegundos { get; set; }
    public int CantidadIntentosFallidos { get; set; }
    public int CantidadAciertos { get; set; }
    public bool CompletadaExitosamente { get; set; }
    public DateTime FechaRegistro { get; set; }
    public string NuevoEstadoAsignacion { get; set; } = string.Empty;
}
