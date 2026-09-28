namespace TeaLearning.Services.DTOs.Achievements;

public class AchievementBadgeDto
{
    public string Clave { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Icono { get; set; } = string.Empty;
    public bool Desbloqueado { get; set; }
    public DateTime? FechaDesbloqueo { get; set; }
}
