namespace TeaLearning.Services.DTOs.Analytics;

public class StudentClassPerformanceDto
{
    public Guid StudentId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string CodigoEstudiante { get; set; } = string.Empty;
    public int ActividadesCompletadas { get; set; }
    public int TotalIntentos { get; set; }
    public double TasaAciertosPorcentaje { get; set; }
    public double PromedioTiempoRespuestaSegundos { get; set; }
    public double IntentosFallidosPromedio { get; set; }
    public List<WeeklyEvolutionDto> EvolucionSemanal { get; set; } = new List<WeeklyEvolutionDto>();
}
