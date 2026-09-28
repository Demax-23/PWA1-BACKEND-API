using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Guardian;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Api.Controllers;

[Authorize(Roles = "Acudiente,Administrador")]
[Route("api/guardian")]
public class GuardianController : BaseApiController
{
    private readonly IGuardianMonitoringService _guardianService;

    public GuardianController(IGuardianMonitoringService guardianService)
    {
        _guardianService = guardianService;
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
            if (result.Message.Contains("autorización", StringComparison.OrdinalIgnoreCase))
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
}
