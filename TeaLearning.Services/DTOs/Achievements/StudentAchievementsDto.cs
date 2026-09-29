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

    // HU-011: Propiedades para la vista "Mis logros" de Interfaz_Estudiante.html
    public List<StudentModuleProgressDto> Modulos { get; set; } = new List<StudentModuleProgressDto>();
    public List<StudentMilestoneDto> Logros { get; set; } = new List<StudentMilestoneDto>();
}

public class StudentModuleProgressDto
{
    public Guid ModuloId { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public int Orden { get; set; }
    public int ActividadesTotales { get; set; }
    public int ActividadesCompletadas { get; set; }
    public bool Completado { get; set; }
    public string? FechaCompletado { get; set; }
}

public class StudentMilestoneDto
{
    public string Id { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? Fecha { get; set; }
}
