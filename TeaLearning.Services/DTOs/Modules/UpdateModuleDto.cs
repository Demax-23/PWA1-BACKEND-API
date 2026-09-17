using System.ComponentModel.DataAnnotations;

namespace TeaLearning.Services.DTOs.Modules;

public class UpdateModuleDto
{
    [Required(ErrorMessage = "El título del módulo es obligatorio")]
    [MaxLength(150, ErrorMessage = "El título no puede superar los 150 caracteres")]
    public string Titulo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción del módulo es obligatoria")]
    [MaxLength(500, ErrorMessage = "La descripción no puede superar los 500 caracteres")]
    public string Descripcion { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Icono { get; set; }

    public int Orden { get; set; } = 1;

    public bool IsActive { get; set; } = true;
}
