namespace TeaLearning.Services.DTOs.Analytics;

public class ActivityTypeMetricDto
{
    public string TipoActividad { get; set; } = string.Empty;
    public int TotalEjecuciones { get; set; }
    public double PromedioTiempoRespuestaSegundos { get; set; }
    public double TasaAciertosPorcentaje { get; set; }
    public double PromedioIntentosFallidos { get; set; }
}
