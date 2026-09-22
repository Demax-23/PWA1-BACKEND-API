using System.ComponentModel.DataAnnotations;

namespace TeaLearning.Services.DTOs.Categories;

public class CreateCategoryDto
{
    [Required(ErrorMessage = "El nombre de la categoría es obligatorio")]
    [MaxLength(150, ErrorMessage = "El nombre no puede superar los 150 caracteres")]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "La descripción no puede superar los 500 caracteres")]
    public string? Descripcion { get; set; }

    [MaxLength(30, ErrorMessage = "El color no puede superar los 30 caracteres")]
    public string? Color { get; set; } = "#2F6E5C";

    public int Orden { get; set; } = 1;

    public bool IsActive { get; set; } = true;
}
