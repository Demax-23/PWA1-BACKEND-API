namespace TeaLearning.Services.DTOs.Analytics;

public class WeeklyEvolutionDto
{
    public string SemanaEtiqueta { get; set; } = string.Empty; // Ej. "Semana 38 - Sep 2026"
    public DateTime FechaInicioSemana { get; set; }
    public int ActividadesCompletadas { get; set; }
    public double TasaAciertosPorcentaje { get; set; }
    public double PromedioTiempoRespuestaSegundos { get; set; }
}
