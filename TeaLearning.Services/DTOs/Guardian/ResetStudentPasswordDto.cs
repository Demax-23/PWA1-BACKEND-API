using System.ComponentModel.DataAnnotations;

namespace TeaLearning.Services.DTOs.Guardian;

public class ResetStudentPasswordDto
{
    [Required(ErrorMessage = "La nueva contraseña es obligatoria")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Debe confirmar la nueva contraseña")]
    [Compare(nameof(NewPassword), ErrorMessage = "Las contraseñas no coinciden")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
