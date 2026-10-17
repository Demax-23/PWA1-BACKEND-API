using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Auth;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Api.Controllers;

[AllowAnonymous]
public class AuthController : BaseApiController
{
    private readonly IAuthService _authService;
    private readonly IPasswordResetService _passwordResetService;

    public AuthController(IAuthService authService, IPasswordResetService passwordResetService)
    {
        _authService = authService;
        _passwordResetService = passwordResetService;
    }

    /// <summary>
    /// Registra un nuevo usuario en la plataforma con su rol asignado.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<AuthResponseDto>.Fail("Datos de registro inválidos", errors));
        }

        var result = await _authService.RegisterAsync(dto);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Inicia sesión con correo electrónico y contraseña generando JWT y Refresh Token.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<AuthResponseDto>.Fail("Datos de acceso inválidos", errors));
        }

        var result = await _authService.LoginAsync(dto);
        if (!result.Success)
        {
            return Unauthorized(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Renueva el token de acceso utilizando un refresh token válido.
    /// </summary>
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<AuthResponseDto>.Fail("Datos de renovación inválidos"));
        }

        var result = await _authService.RefreshTokenAsync(dto);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Solicita el enlace de restablecimiento de contraseña por correo (Administrador, Docente y Acudiente).
    /// Responde siempre igual, exista o no la cuenta, para no revelar qué correos están registrados.
    /// </summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("password-reset-request")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<bool>.Fail("Datos de solicitud inválidos", errors));
        }

        var result = await _passwordResetService.ForgotPasswordAsync(dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Define una nueva contraseña usando el token recibido por correo. El token es de un solo uso y de vigencia corta.
    /// </summary>
    [HttpPost("reset-password")]
    [EnableRateLimiting("password-reset-confirm")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<bool>.Fail("Datos de restablecimiento inválidos", errors));
        }

        var result = await _passwordResetService.ResetPasswordAsync(dto, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
