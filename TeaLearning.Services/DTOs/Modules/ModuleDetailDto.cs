using TeaLearning.Services.DTOs.Activities;

namespace TeaLearning.Services.DTOs.Modules;

public class ModuleDetailDto
{
    public Guid Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? Icono { get; set; }
    public int Orden { get; set; }
    public bool IsActive { get; set; }
    public List<ActivitySummaryDto> Activities { get; set; } = new List<ActivitySummaryDto>();
}
