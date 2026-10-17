using System.ComponentModel.DataAnnotations;

namespace TeaLearning.Services.DTOs.Auth;

public class ResetPasswordRequestDto
{
    [Required(ErrorMessage = "El correo electrónico es obligatorio")]
    [EmailAddress(ErrorMessage = "Formato de correo electrónico inválido")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Token de Identity codificado en Base64Url (tal como llega en el enlace del correo).</summary>
    [Required(ErrorMessage = "El enlace de restablecimiento no es válido")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contraseña es obligatoria")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Debe confirmar la nueva contraseña")]
    [Compare(nameof(NewPassword), ErrorMessage = "Las contraseñas no coinciden")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
