using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Auth;

namespace TeaLearning.Services.Interfaces;

public interface IPasswordResetService
{
    /// <summary>
    /// Envía el enlace de restablecimiento si la cuenta existe, está activa y su rol lo permite.
    /// Siempre retorna la misma respuesta para no revelar qué correos están registrados.
    /// </summary>
    Task<ApiResponse<bool>> ForgotPasswordAsync(ForgotPasswordRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>Define la nueva contraseña a partir del token recibido por correo.</summary>
    Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordRequestDto dto, CancellationToken cancellationToken = default);
}
