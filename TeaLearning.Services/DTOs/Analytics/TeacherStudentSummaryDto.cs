namespace TeaLearning.Services.DTOs.Analytics;

public class TeacherStudentSummaryDto
{
    public Guid StudentId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string CodigoEstudiante { get; set; } = string.Empty;
    public string ClassroomNombre { get; set; } = string.Empty;

    // Métricas globales
    public double TasaAciertosGlobal { get; set; } // Porcentaje 0 a 100
    public double PromedioTiempoRespuestaSegundos { get; set; }
    public int TotalActividadesCompletadas { get; set; }
    public int TotalActividadesPendientes { get; set; }
    public int TotalActividadesEnProgreso { get; set; }

    // Desgloses analíticos
    public List<ActivityTypeMetricDto> TiemposPorTipoActividad { get; set; } = new List<ActivityTypeMetricDto>();
    public List<AttemptsCurveDto> CurvaIntentos { get; set; } = new List<AttemptsCurveDto>();
    public List<WeeklyEvolutionDto> EvolucionSemanal { get; set; } = new List<WeeklyEvolutionDto>();
}
