using TeaLearning.Core.Common;
using TeaLearning.Core.Enums;

namespace TeaLearning.Core.Entities;

public class SensoryPreference : BaseEntity
{
    public Guid StudentProfileId { get; set; }
    public virtual StudentProfile StudentProfile { get; set; } = null!;

    // Paleta de colores adaptativa (ej. "PastelSuave", "Neutro", "EscalaGrises")
    public string PaletaColor { get; set; } = "PastelSuave";

    // Tipografía adaptada para lectura amigable (ej. "Atkinson Hyperlegible", "OpenDyslexic", "Arial", "Verdana")
    public string Tipografia { get; set; } = "Atkinson Hyperlegible";

    // Mayor espacio entre letras para legibilidad adaptativa
    public bool MayorEspacioEntreLetras { get; set; } = false;

    // Retroalimentación sonora en interacciones
    public bool AudioActivo { get; set; } = true;

    // Supresión de transiciones bruscas o destellos
    public bool AnimacionesReducidas { get; set; } = true;

    // Escala de tamaño tipográfico (1: Pequeño, 2: Normal, 3: Grande, 4: Muy Grande)
    public int NivelEscalaFuente { get; set; } = 2;

    // Modo de contraste visual
    public ModoContraste ModoContraste { get; set; } = ModoContraste.Estandar;

    // Previene ansiedad por competencia temporal
    public bool OcultarTemporizadores { get; set; } = true;
}
