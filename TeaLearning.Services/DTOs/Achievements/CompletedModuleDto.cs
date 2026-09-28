namespace TeaLearning.Services.DTOs.Achievements;

public class CompletedModuleDto
{
    public Guid ModuloId { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public int TotalActividades { get; set; }
    public DateTime FechaConclusion { get; set; }
}
