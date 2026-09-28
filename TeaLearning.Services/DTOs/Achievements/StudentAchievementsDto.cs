namespace TeaLearning.Services.DTOs.Achievements;

public class StudentAchievementsDto
{
    public Guid EstudianteId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public int TotalActividadesCompletadas { get; set; }
    public int TotalModulosConcluidos { get; set; }
    public List<AchievementBadgeDto> Insignias { get; set; } = new List<AchievementBadgeDto>();
    public List<CompletedModuleDto> ModulosConcluidos { get; set; } = new List<CompletedModuleDto>();
    public string MensajeMotivacional { get; set; } = string.Empty;
}
