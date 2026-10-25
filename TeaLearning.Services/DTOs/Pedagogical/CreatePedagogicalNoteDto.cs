using System.ComponentModel.DataAnnotations;

namespace TeaLearning.Services.DTOs.Pedagogical;

public class CreatePedagogicalNoteDto : IValidatableObject
{
    private static readonly HashSet<string> AllowedTipos = new(StringComparer.OrdinalIgnoreCase)
    {
        "Avance", "Recomendacion", "Observacion", "AspectoAReforzar",
        "Conductual", "Sensorial", "Logro", "Alerta", "General"
    };

    private static readonly HashSet<string> AllowedAreas = new(StringComparer.OrdinalIgnoreCase)
    {
        "Academica", "Comunicacion", "Socioemocional", "Autonomia",
        "Sensorial", "Cognitiva", "Social", "Regulacion", "General"
    };

    [Required(ErrorMessage = "El contenido de la observación pedagógica es obligatorio")]
    [MaxLength(2000, ErrorMessage = "La observación pedagógica no puede superar los 2000 caracteres")]
    public string Contenido { get; set; } = string.Empty;

    [MaxLength(100, ErrorMessage = "El título de la observación no puede superar los 100 caracteres")]
    public string? Titulo { get; set; }

    [MaxLength(30, ErrorMessage = "El tipo de observación no puede superar los 30 caracteres")]
    public string? Tipo { get; set; }

    [MaxLength(30, ErrorMessage = "El área de observación no puede superar los 30 caracteres")]
    public string? Area { get; set; }

    [MaxLength(500, ErrorMessage = "La recomendación para casa no puede superar los 500 caracteres")]
    public string? RecomendacionCasa { get; set; }

    public bool? VisibleAcudiente { get; set; }

    [MaxLength(40, ErrorMessage = "El formato de la fecha no es válido")]
    public string? Fecha { get; set; }

    public Guid? AssignmentId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Contenido))
        {
            yield return new ValidationResult("El contenido de la observación pedagógica no puede estar vacío.", [nameof(Contenido)]);
        }

        if (!string.IsNullOrWhiteSpace(Tipo) && !AllowedTipos.Contains(Tipo.Trim()))
        {
            yield return new ValidationResult("El tipo de observación especificado no es válido.", [nameof(Tipo)]);
        }

        if (!string.IsNullOrWhiteSpace(Area) && !AllowedAreas.Contains(Area.Trim()))
        {
            yield return new ValidationResult("El área de observación especificada no es válida.", [nameof(Area)]);
        }
    }
}
