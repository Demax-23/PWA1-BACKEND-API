using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Reports;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Api.Controllers;

/// <summary>
/// Controlador para la generación de reportes analíticos en PDF (Docente y Acudiente)
/// y ejecución de procedimientos almacenados de diagnóstico y reconciliación TEA.
/// </summary>
[Authorize]
public class ReportsController : BaseApiController
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// Genera el reporte integral individual de un estudiante mediante sp_GenerarReporteIntegralEstudiante.
    /// Si quien consulta es Acudiente, valida vinculación familiar y filtra notas privadas (VisibleAcudiente = 1).
    /// </summary>
    [HttpGet("student/{studentId:guid}")]
    [Authorize(Roles = "Docente,Acudiente,Administrador")]
    [ProducesResponseType(typeof(ApiResponse<StudentComprehensiveReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StudentComprehensiveReportDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<StudentComprehensiveReportDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentReport(
        Guid studentId,
        [FromQuery] int diasVentana = 30,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteStudentReportRequestAsync(studentId, diasVentana, cancellationToken);
    }

    /// <summary>
    /// Genera el reporte integral del estudiante vinculado al usuario actual (ideal para el rol Acudiente)
    /// o del estudiante indicado por parámetro opcional.
    /// </summary>
    [HttpGet("student")]
    [Authorize(Roles = "Docente,Acudiente,Administrador")]
    [ProducesResponseType(typeof(ApiResponse<StudentComprehensiveReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StudentComprehensiveReportDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<StudentComprehensiveReportDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentOrSelectedStudentReport(
        [FromQuery] Guid? studentId = null,
        [FromQuery] int diasVentana = 30,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteStudentReportRequestAsync(studentId, diasVentana, cancellationToken);
    }

    private async Task<IActionResult> ExecuteStudentReportRequestAsync(
        Guid? studentId,
        int diasVentana,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<StudentComprehensiveReportDto>.Fail("No se pudo identificar al usuario autenticado."));
        }

        bool isStaff = User.IsInRole("Administrador") || User.IsInRole("Docente");
        bool isGuardianOnly = !isStaff && User.IsInRole("Acudiente");

        var result = await _reportService.GetStudentComprehensiveReportAsync(
            studentId,
            userId.Value,
            isGuardianOnly,
            diasVentana,
            cancellationToken);

        if (!result.Success)
        {
            if (result.Message != null && result.Message.Contains("autorización", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, result);
            }
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Genera el reporte analítico consolidado de un aula específica mediante sp_GenerarReporteConsolidadoAula.
    /// </summary>
    [HttpGet("classroom/{classroomId:guid}")]
    [Authorize(Roles = "Docente,Administrador")]
    [ProducesResponseType(typeof(ApiResponse<ClassroomConsolidatedReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ClassroomConsolidatedReportDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClassroomReportById(
        Guid classroomId,
        [FromQuery] int diasVentana = 30,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<ClassroomConsolidatedReportDto>.Fail("No se pudo identificar al usuario autenticado."));
        }

        var result = await _reportService.GetClassroomConsolidatedReportAsync(
            classroomId,
            userId.Value,
            diasVentana,
            cancellationToken);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Genera el reporte analítico consolidado del aula titular del docente autenticado mediante sp_GenerarReporteConsolidadoAula.
    /// </summary>
    [HttpGet("classroom")]
    [Authorize(Roles = "Docente,Administrador")]
    [ProducesResponseType(typeof(ApiResponse<ClassroomConsolidatedReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ClassroomConsolidatedReportDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyClassroomReport(
        [FromQuery] Guid? classroomId = null,
        [FromQuery] int diasVentana = 30,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<ClassroomConsolidatedReportDto>.Fail("No se pudo identificar al usuario autenticado."));
        }

        var result = await _reportService.GetClassroomConsolidatedReportAsync(
            classroomId,
            userId.Value,
            diasVentana,
            cancellationToken);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Ejecuta el motor clínico-pedagógico sp_EvaluarAlertasYRecomendacionesTEA para detectar patrones
    /// de frustración, latencia elevada, pausas prolongadas o logros sostenidos.
    /// </summary>
    [HttpPost("alerts/evaluate")]
    [Authorize(Roles = "Docente,Administrador")]
    [ProducesResponseType(typeof(ApiResponse<List<TeaAlertEvaluationItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> EvaluateAlerts(
        [FromBody] EvaluateTeaAlertsRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<List<TeaAlertEvaluationItemDto>>.Fail("No se pudo identificar al usuario autenticado."));
        }

        var result = await _reportService.EvaluateTeaAlertsAndRecommendationsAsync(
            userId.Value,
            request ?? new EvaluateTeaAlertsRequestDto(),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Reconcilia y recalcula el progreso de asignaciones, módulos e insignias de un estudiante
    /// ejecutando sp_RecalcularProgresoYLogrosEstudiante.
    /// </summary>
    [HttpPost("student/{studentId:guid}/recalculate-progress")]
    [Authorize(Roles = "Docente,Administrador,Acudiente")]
    [ProducesResponseType(typeof(ApiResponse<RecalculateStudentProgressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<RecalculateStudentProgressDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecalculateStudentProgress(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var result = await _reportService.RecalculateStudentProgressAndAchievementsAsync(studentId, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }
}
