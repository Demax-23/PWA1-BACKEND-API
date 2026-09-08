using System.ComponentModel.DataAnnotations;

namespace TeaLearning.Services.DTOs.Auth;

public class RefreshTokenRequestDto
{
    [Required(ErrorMessage = "El token de acceso es obligatorio")]
    public string AccessToken { get; set; } = string.Empty;

    [Required(ErrorMessage = "El refresh token es obligatorio")]
    public string RefreshToken { get; set; } = string.Empty;
}
