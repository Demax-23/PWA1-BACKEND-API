using System.ComponentModel.DataAnnotations;

namespace TeaLearning.Services.DTOs.Assignments;

public class GradeAssignmentDto
{
    [Required(ErrorMessage = "La calificación es obligatoria.")]
    [Range(typeof(decimal), "1.0", "5.0", ParseLimitsInInvariantCulture = true, ErrorMessage = "La calificación debe estar entre 1.0 y 5.0.")]
    public decimal Calificacion { get; set; }

    [MaxLength(500, ErrorMessage = "El comentario pedagógico no puede exceder los 500 caracteres.")]
    public string? Comentario { get; set; }
}
