using System.ComponentModel.DataAnnotations;

namespace TeaLearning.Services.DTOs.Pedagogical;

public class CreatePedagogicalNoteDto
{
    [Required(ErrorMessage = "El contenido de la observación pedagógica es obligatorio")]
    [MaxLength(2000, ErrorMessage = "La observación pedagógica no puede superar los 2000 caracteres")]
    public string Contenido { get; set; } = string.Empty;
}
