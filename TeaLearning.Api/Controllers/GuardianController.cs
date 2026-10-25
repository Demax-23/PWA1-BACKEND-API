using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Guardian;
using TeaLearning.Services.DTOs.Pedagogical;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Api.Controllers;

[Authorize(Roles = "Acudiente,Administrador")]
[Route("api/guardian")]
public class GuardianController : BaseApiController
{
    private readonly IGuardianMonitoringService _guardianService;
    private readonly IPedagogicalNoteService _noteService;
    private readonly IUserService _userService;

    public GuardianController(
        IGuardianMonitoringService guardianService,
        IPedagogicalNoteService noteService,
        IUserService userService)
    {
        _guardianService = guardianService;
        _noteService = noteService;
        _userService = userService;
    }

    /// <summary>
    /// Consulta consolidada del progreso pedagógico del estudiante para su acudiente o tutor legal (HU-009).
    /// Expone actividades completadas vs pendientes, tiempo total invertido, historial reciente y orientación pedagógica.
    /// </summary>
    [HttpGet("students/{studentId:guid}/progress")]
    [HttpGet("/api/students/{studentId:guid}/progress")]
    [ProducesResponseType(typeof(ApiResponse<GuardianStudentProgressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<GuardianStudentProgressDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<GuardianStudentProgressDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<GuardianStudentProgressDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentProgress([FromRoute] Guid studentId, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized(ApiResponse<GuardianStudentProgressDto>.Fail("No se pudo identificar al usuario autenticado."));
        }

        bool isAdmin = User.IsInRole("Administrador");
        var result = await _guardianService.GetStudentProgressAsync(studentId, currentUserId.Value, isAdmin, cancellationToken);
        if (!result.Success)
        {
            if ((result.Message ?? string.Empty).Contains("autorización", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, result);
            }
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Consulta paginada del historial de ejecuciones de actividades del estudiante para su acudiente (HU-009).
    /// </summary>
    [HttpGet("students/{studentId:guid}/activity-history")]
    [HttpGet("/api/students/{studentId:guid}/activity-history")]
    [ProducesResponseType(typeof(ApiResponse<ActivityHistoryPagedDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ActivityHistoryPagedDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<ActivityHistoryPagedDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<ActivityHistoryPagedDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentActivityHistory(
        [FromRoute] Guid studentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized(ApiResponse<ActivityHistoryPagedDto>.Fail("No se pudo identificar al usuario autenticado."));
        }

        bool isAdmin = User.IsInRole("Administrador");
        var result = await _guardianService.GetActivityHistoryAsync(studentId, currentUserId.Value, isAdmin, page, pageSize, cancellationToken);
        if (!result.Success)
        {
            if (result.Message.Contains("autorización", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, result);
            }
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Consulta paginada y optimizada de observaciones pedagógicas del estudiante para su acudiente (HU-012).
    /// </summary>
    [HttpGet("students/{studentId:guid}/observations")]
    [HttpGet("/api/students/{studentId:guid}/observations")]
    [ProducesResponseType(typeof(ApiResponse<ObservationsPagedDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ObservationsPagedDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<ObservationsPagedDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<ObservationsPagedDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetObservations(
        [FromRoute] Guid studentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? tipo = null,
        [FromQuery] string? area = null,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized(ApiResponse<ObservationsPagedDto>.Fail("No se pudo identificar al usuario autenticado."));
        }

        bool isAdmin = User.IsInRole("Administrador");
        var result = await _noteService.GetObservationsPagedForGuardianAsync(
            studentId, currentUserId.Value, isAdmin, page, pageSize, tipo, area, cancellationToken);

        if (!result.Success)
        {
            if (result.Message.Contains("autorización", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, result);
            }
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Restablece la contraseña de un estudiante vinculado. Accesible para el acudiente titular o Administrador.
    /// La nueva contraseña debe cumplir la política de Identity (mínimo 6 caracteres y 1 dígito).
    /// </summary>
    [HttpPost("students/{studentId:guid}/reset-password")]
    [Authorize(Roles = "Acudiente,Administrador")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetStudentPassword(
        [FromRoute] Guid studentId,
        [FromBody] ResetStudentPasswordDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<bool>.Fail("Datos de contraseña inválidos", errors));
        }

        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized(ApiResponse<bool>.Fail("No se pudo identificar al usuario autenticado."));
        }

        bool isAdmin = User.IsInRole("Administrador");
        var result = await _userService.ResetStudentPasswordAsync(currentUserId.Value, studentId, dto.NewPassword, isAdmin);
        if (!result.Success)
        {
            var msg = result.Message ?? string.Empty;
            if (msg.Contains("autorización", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, result);
            }
            if (msg.Contains("no encontrado", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(result);
            }
            return BadRequest(result);
        }

        return Ok(result);
    }
}
