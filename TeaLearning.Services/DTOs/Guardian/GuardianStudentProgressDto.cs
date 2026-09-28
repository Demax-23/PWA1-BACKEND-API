namespace TeaLearning.Services.DTOs.Guardian;

public class GuardianStudentProgressDto
{
    public Guid StudentId { get; set; }
    public string StudentNombre { get; set; } = string.Empty;
    public string CodigoEstudiante { get; set; } = string.Empty;

    // Resumen de actividades
    public int ActividadesAsignadasTotal { get; set; }
    public int ActividadesCompletadas { get; set; }
    public int ActividadesEnProgreso { get; set; }
    public int ActividadesPendientes { get; set; }
    public double PorcentajeAvance { get; set; } // 0 a 100%

    // Tiempo total invertido acumulado
    public double TiempoTotalInvertidoMinutos { get; set; }

    // Evolución y últimas actividades
    public List<RecentActivityDto> UltimasActividades { get; set; } = new List<RecentActivityDto>();
    public string MensajePedagogico { get; set; } = string.Empty;
}
