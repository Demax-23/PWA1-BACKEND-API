using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Reports;

namespace TeaLearning.Services.Interfaces;

/// <summary>
/// Servicio de reportes analíticos y orquestación de los 5 Stored Procedures de negocio de TeaLearning.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Invoca sp_GenerarReporteIntegralEstudiante para construir el reporte individual completo
    /// (con series temporales, desglose por tipo de actividad, avance de módulos, calificaciones y bitácora).
    /// </summary>
    Task<ApiResponse<StudentComprehensiveReportDto>> GetStudentComprehensiveReportAsync(
        Guid? studentProfileId,
        Guid requesterUserId,
        bool isGuardian,
        int diasVentana = 30,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invoca sp_GenerarReporteConsolidadoAula para construir el reporte analítico consolidado del aula.
    /// </summary>
    Task<ApiResponse<ClassroomConsolidatedReportDto>> GetClassroomConsolidatedReportAsync(
        Guid? classroomId,
        Guid requesterUserId,
        int diasVentana = 30,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invoca sp_AsignarRutinaAdaptativaPorAula para asignar automáticamente actividades adaptativas a los estudiantes del aula.
    /// </summary>
    Task<ApiResponse<List<AdaptiveRoutineAssignmentResultDto>>> AssignAdaptiveRoutineByClassroomAsync(
        Guid requesterUserId,
        AdaptiveRoutineRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invoca sp_EvaluarAlertasYRecomendacionesTEA para detectar patrones de frustración, fatiga o logro en el aula.
    /// </summary>
    Task<ApiResponse<List<TeaAlertEvaluationItemDto>>> EvaluateTeaAlertsAndRecommendationsAsync(
        Guid requesterUserId,
        EvaluateTeaAlertsRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invoca sp_RecalcularProgresoYLogrosEstudiante para reconciliar estados de asignaciones, módulos e insignias.
    /// </summary>
    Task<ApiResponse<RecalculateStudentProgressDto>> RecalculateStudentProgressAndAchievementsAsync(
        Guid studentProfileId,
        CancellationToken cancellationToken = default);
}
