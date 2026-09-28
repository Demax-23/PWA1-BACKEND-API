using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Analytics;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Api.Controllers;

[Authorize(Roles = "Docente,Administrador")]
[Route("api/analytics")]
public class AnalyticsController : BaseApiController
{
    private readonly ITeacherAnalyticsService _analyticsService;

    public AnalyticsController(ITeacherAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    /// <summary>
    /// Obtiene las métricas agregadas y analítica cognitiva de un estudiante (HU-008).
    /// Incluye tasa de aciertos global, promedio de tiempo de respuesta por tipo de actividad, curva de intentos y evolución semanal.
    /// </summary>
    [HttpGet("teacher/students/{id:guid}/summary")]
    [ProducesResponseType(typeof(ApiResponse<TeacherStudentSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TeacherStudentSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentSummary([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await _analyticsService.GetStudentAnalyticsSummaryAsync(id, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Obtiene el resumen consolidado de desempeño pedagógico a nivel de aula (HU-008).
    /// Incluye tasa de aciertos promedio, tiempo de respuesta promedio y listado de rendimiento por estudiante.
    /// </summary>
    [HttpGet("teacher/classes/{classId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TeacherClassroomSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TeacherClassroomSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClassroomSummary([FromRoute] Guid classId, CancellationToken cancellationToken)
    {
        var result = await _analyticsService.GetClassroomAnalyticsSummaryAsync(classId, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }
}
