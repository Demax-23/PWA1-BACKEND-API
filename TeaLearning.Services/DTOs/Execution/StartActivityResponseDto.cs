using TeaLearning.Services.DTOs.Sensory;

namespace TeaLearning.Services.DTOs.Execution;

public class StartActivityResponseDto
{
    public Guid AssignmentId { get; set; }
    public Guid ActivityId { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string TipoActividad { get; set; } = string.Empty;
    public string ObjetivoPedagogico { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = "{}";
    public string Estado { get; set; } = string.Empty;
    public DateTime FechaInicio { get; set; }

    // Preferencias sensoriales del estudiante para adaptar la interfaz
    public SensoryPreferenceDto? SensoryAdaptations { get; set; }
}
