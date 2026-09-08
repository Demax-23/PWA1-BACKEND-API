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
    public string Tipografia { get; set; } = "OpenDyslexic";

    public bool AudioActivo { get; set; }

    public bool AnimacionesReducidas { get; set; }

    [Range(1, 4, ErrorMessage = "El nivel de escala de fuente debe estar entre 1 y 4")]
    public int NivelEscalaFuente { get; set; } = 2;

    public ModoContraste ModoContraste { get; set; } = ModoContraste.Estandar;

    public bool OcultarTemporizadores { get; set; }
}
