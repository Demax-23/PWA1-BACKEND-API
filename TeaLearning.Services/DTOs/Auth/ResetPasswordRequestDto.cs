using System.ComponentModel.DataAnnotations;
using System.Text;

namespace TeaLearning.Services.DTOs.Auth;

public class ResetPasswordRequestDto : IValidatableObject
{
    [Required(ErrorMessage = "El correo electrónico es obligatorio")]
    [EmailAddress(ErrorMessage = "Formato de correo electrónico inválido")]
    [MaxLength(256, ErrorMessage = "El correo electrónico no puede exceder los 256 caracteres")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Token de Identity codificado en Base64Url (tal como llega en el enlace del correo).</summary>
    [Required(ErrorMessage = "El enlace de restablecimiento no es válido")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contraseña es obligatoria")]
    [MinLength(6, ErrorMessage = "La nueva contraseña debe tener al menos 6 caracteres")]
    [MaxLength(72, ErrorMessage = "La nueva contraseña no puede superar los 72 caracteres")]
    [RegularExpression(@"^.*\d.*$", ErrorMessage = "La nueva contraseña debe incluir al menos un número")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Debe confirmar la nueva contraseña")]
    [Compare(nameof(NewPassword), ErrorMessage = "Las contraseñas no coinciden")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrEmpty(NewPassword) && Encoding.UTF8.GetByteCount(NewPassword) > 72)
        {
            yield return new ValidationResult("La nueva contraseña supera el límite máximo permitido de 72 bytes.", [nameof(NewPassword)]);
        }
    }
}
