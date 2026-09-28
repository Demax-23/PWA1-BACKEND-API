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
    public List<GuardianWeeklyProgressDto> Historico { get; set; } = new List<GuardianWeeklyProgressDto>();
    public string MensajePedagogico { get; set; } = string.Empty;

    public object Resumen => new
    {
        ActividadesAsignadas = ActividadesAsignadasTotal,
        ActividadesCompletadas = ActividadesCompletadas,
        TotalIntentos = UltimasActividades.Count,
        TasaAciertos = UltimasActividades.Any() && UltimasActividades.Sum(u => u.CantidadAciertos + u.CantidadIntentosFallidos) > 0
            ? Math.Round(100.0 * UltimasActividades.Sum(u => u.CantidadAciertos) / UltimasActividades.Sum(u => u.CantidadAciertos + u.CantidadIntentosFallidos), 1)
            : 100.0,
        TiempoPromedioSegundos = UltimasActividades.Any() ? Math.Round(UltimasActividades.Average(u => u.TiempoRespuestaSegundos), 1) : (TiempoTotalInvertidoMinutos * 60.0),
        DiasActivos = UltimasActividades.Select(u => u.FechaRegistro.Date).Distinct().Count(),
        UltimaActividad = UltimasActividades.FirstOrDefault()?.FechaRegistro
    };
}

public class GuardianWeeklyProgressDto
{
    public string Periodo { get; set; } = string.Empty;
    public int ActividadesCompletadas { get; set; }
    public double TasaAciertos { get; set; }
}

public class RecentActivityHistoryDto
{
    public string Titulo { get; set; } = string.Empty;
    public string TipoActividad { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
    public bool CompletadaExitosamente { get; set; }
    public int CantidadAciertos { get; set; }
    public int CantidadIntentosFallidos { get; set; }
    public int TiempoRespuestaSegundos { get; set; }
}

public class ActivityHistoryPagedDto
{
    public List<RecentActivityHistoryDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
