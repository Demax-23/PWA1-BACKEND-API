using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TeaLearning.Core.Entities;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Auth;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Services.Services;

/// <summary>
/// Restablecimiento de contraseña por correo para Administrador, Docente y Acudiente.
/// El rol Estudiante queda fuera: su contraseña la restablece su acudiente desde la plataforma.
/// </summary>
public class PasswordResetService : IPasswordResetService
{
    private static readonly string[] EmailResetRoles = ["Administrador", "Docente", "Acudiente"];

    private const string GenericForgotMessage =
        "Si el correo pertenece a una cuenta de Administrador, Docente o Acudiente, recibirás un mensaje con instrucciones.";

    private const string InvalidLinkMessage = "El enlace no es válido o ya expiró. Solicita uno nuevo.";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<PasswordResetService> logger)
    {
        _userManager = userManager;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ApiResponse<bool>> ForgotPasswordAsync(ForgotPasswordRequestDto dto, CancellationToken cancellationToken = default)
    {
        var generic = ApiResponse<bool>.Ok(true, GenericForgotMessage);

        var user = await FindEligibleUserAsync(dto.Email);
        if (user == null)
        {
            return generic;
        }

        try
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var configuredBaseUrl = _configuration["Frontend:BaseUrl"];
            var baseUrl = (!string.IsNullOrWhiteSpace(configuredBaseUrl) ? configuredBaseUrl : "http://127.0.0.1:5500").Trim().TrimEnd('/');

            // El token va en el fragmento (#): el navegador nunca lo envía al servidor ni queda en logs o cabeceras Referer.
            var link = $"{baseUrl}/reset-password.html#email={Uri.EscapeDataString(user.Email!)}&token={Base64Url.Encode(token)}";

            var (subject, html, text) = PasswordResetEmailTemplates.BuildResetLink(user.NombreCompleto, link, GetTokenLifespanMinutes());
            await _emailService.SendAsync(user.Email!, user.NombreCompleto, subject, html, text, cancellationToken);
        }
        catch (Exception ex)
        {
            // El fallo se registra pero no se expone: revelarlo permitiría deducir si la cuenta existe.
            _logger.LogError(ex, "No fue posible enviar el correo de restablecimiento de contraseña.");
        }

        return generic;
    }

    public async Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordRequestDto dto, CancellationToken cancellationToken = default)
    {
        var user = await FindEligibleUserAsync(dto.Email);
        var token = Base64Url.TryDecode(dto.Token);
        if (user == null || token == null)
        {
            return ApiResponse<bool>.Fail(InvalidLinkMessage);
        }

        var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == IdentityErrorTranslator.InvalidTokenCode))
            {
                return ApiResponse<bool>.Fail(InvalidLinkMessage);
            }

            return ApiResponse<bool>.Fail(
                "No fue posible actualizar la contraseña.",
                IdentityErrorTranslator.Translate(result.Errors));
        }

        // Cierra las sesiones renovables existentes: quien tuviera la clave anterior no puede seguir renovando su acceso.
        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        await _userManager.UpdateAsync(user);

        try
        {
            var (subject, html, text) = PasswordResetEmailTemplates.BuildPasswordChanged(user.NombreCompleto);
            await _emailService.SendAsync(user.Email!, user.NombreCompleto, subject, html, text, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "La contraseña se actualizó pero no se pudo enviar el correo de confirmación.");
        }

        return ApiResponse<bool>.Ok(true, "Tu contraseña fue actualizada. Ya puedes iniciar sesión.");
    }

    /// <summary>Retorna el usuario solo si existe, está activo y tiene un rol habilitado para el flujo por correo.</summary>
    private async Task<ApplicationUser?> FindEligibleUserAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user == null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email))
        {
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);
        return roles.Any(r => EmailResetRoles.Contains(r)) ? user : null;
    }

    private int GetTokenLifespanMinutes()
    {
        return int.TryParse(_configuration["PasswordReset:TokenLifespanMinutes"], out var minutes) && minutes > 0
            ? minutes
            : 30;
    }
}
