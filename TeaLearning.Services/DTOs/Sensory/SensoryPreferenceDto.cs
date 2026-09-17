using TeaLearning.Core.Enums;

namespace TeaLearning.Services.DTOs.Sensory;

public class SensoryPreferenceDto
{
    public Guid Id { get; set; }
    public Guid StudentProfileId { get; set; }
    public string PaletaColor { get; set; } = "PastelSuave";
    public string Tipografia { get; set; } = "OpenDyslexic";
    public bool AudioActivo { get; set; }
    public int VolumenAudio { get; set; } = 60;
    public bool TextoAVoz { get; set; } = true;
    public bool AnimacionesReducidas { get; set; }
    public int NivelEscalaFuente { get; set; }
    public ModoContraste ModoContraste { get; set; }
    public bool OcultarTemporizadores { get; set; }
    public bool MayorEspacioEntreLetras { get; set; }
}
