using System.ComponentModel.DataAnnotations;

namespace TeaLearning.Services.DTOs.Auth;

public class ForgotPasswordRequestDto
{
    [Required(ErrorMessage = "El correo electrónico es obligatorio")]
    [EmailAddress(ErrorMessage = "Formato de correo electrónico inválido")]
    public string Email { get; set; } = string.Empty;
}
