namespace TeaLearning.Services.DTOs.Activities;

public class ActivitySummaryDto
{
    public Guid Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string TipoActividad { get; set; } = string.Empty;
    public string ObjetivoPedagogico { get; set; } = string.Empty;
    public Guid ModuloId { get; set; }
}
