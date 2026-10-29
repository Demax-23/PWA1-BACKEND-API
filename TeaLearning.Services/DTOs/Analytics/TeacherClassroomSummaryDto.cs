namespace TeaLearning.Services.DTOs.Analytics;

public class TeacherClassroomSummaryDto
{
    public Guid ClassroomId { get; set; }
    public string ClassroomNombre { get; set; } = string.Empty;
    public string Grado { get; set; } = string.Empty;
    public int TotalEstudiantes { get; set; }

    // Métricas del aula
    public double TasaAciertosPromedioAula { get; set; }
    public double PromedioTiempoRespuestaAula { get; set; }
    public int TotalActividadesCompletadasAula { get; set; }
    public int TotalIntentosAula { get; set; }
    public double PromedioIntentosFallidosAula { get; set; }

    public List<ActivityTypeMetricDto> TiemposPorTipoActividad { get; set; } = new List<ActivityTypeMetricDto>();
    public List<StudentClassPerformanceDto> DesempenoEstudiantes { get; set; } = new List<StudentClassPerformanceDto>();
}
