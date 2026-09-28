namespace TeaLearning.Services.DTOs.Guardian;

public class RecentActivityDto
{
    public string ActivityTitulo { get; set; } = string.Empty;
    public string TipoActividad { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
    public bool CompletadaExitosamente { get; set; }
    public int CantidadAciertos { get; set; }
    public int CantidadIntentosFallidos { get; set; }
    public int TiempoRespuestaSegundos { get; set; }
}
