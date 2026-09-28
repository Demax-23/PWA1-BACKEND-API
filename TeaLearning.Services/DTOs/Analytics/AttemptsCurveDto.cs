namespace TeaLearning.Services.DTOs.Analytics;

public class AttemptsCurveDto
{
    public DateTime Fecha { get; set; }
    public string FechaFormato => Fecha.ToString("yyyy-MM-dd");
    public int TotalIntentosFallidos { get; set; }
    public int TotalAciertos { get; set; }
    public double PromedioIntentosFallidosPorActividad { get; set; }
    public int ActividadesEjecutadas { get; set; }
}
