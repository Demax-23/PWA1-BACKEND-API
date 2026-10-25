using System.ComponentModel.DataAnnotations;
using TeaLearning.Core.Enums;

namespace TeaLearning.Services.DTOs.Sensory;

public class UpdateSensoryPreferenceDto
{
    [Required(ErrorMessage = "La paleta de color es obligatoria")]
    [MaxLength(50)]
    public string PaletaColor { get; set; } = "PastelSuave";

    [Required(ErrorMessage = "La tipografía es obligatoria")]
    [MaxLength(50)]
    public string Tipografia { get; set; } = "Atkinson Hyperlegible";

    public bool AudioActivo { get; set; }

    [Range(0, 100, ErrorMessage = "El volumen de audio debe estar entre 0 y 100")]
    public int? VolumenAudio { get; set; }

    public bool? TextoAVoz { get; set; }

    public bool AnimacionesReducidas { get; set; }

    [Range(1, 4, ErrorMessage = "El nivel de escala de fuente debe estar entre 1 y 4")]
    public int NivelEscalaFuente { get; set; } = 2;

    public ModoContraste ModoContraste { get; set; } = ModoContraste.Estandar;

    public bool OcultarTemporizadores { get; set; }

    [MaxLength(30, ErrorMessage = "El modo de temporizador no puede exceder los 30 caracteres")]
    [RegularExpression(@"^(sin-temporizador|tiempo-extendido|estandar)$", ErrorMessage = "Modo de temporizador inválido")]
    public string? ModoTemporizador { get; set; }

    [Range(0, 60, ErrorMessage = "Los segundos de aviso previo deben estar entre 0 y 60")]
    public int? SegundosAvisoPrevio { get; set; }

    public bool MayorEspacioEntreLetras { get; set; }
}
