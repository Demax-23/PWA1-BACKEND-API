using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeaLearning.Core.Enums;
using TeaLearning.Data.Repositories;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Achievements;
using TeaLearning.Services.DTOs.Students;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Api.Controllers;

[Authorize]
public class StudentsController : BaseApiController
{
    private readonly IStudentService _studentService;
    private readonly IAchievementService _achievementService;
    private readonly IActivityAssignmentRepository _assignmentRepository;

    public StudentsController(
        IStudentService studentService,
        IAchievementService achievementService,
        IActivityAssignmentRepository assignmentRepository)
    {
        _studentService = studentService;
        _achievementService = achievementService;
        _assignmentRepository = assignmentRepository;
    }

    /// <summary>
    /// Retorna la rutina visual de actividades asignadas al estudiante autenticado para la fecha actual, ordenadas por secuencia pedagógica (HU-002).
    /// </summary>
    [HttpGet("me/daily-routine")]
    [Authorize(Roles = "Estudiante")]
    [ProducesResponseType(typeof(ApiResponse<List<DailyRoutineItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<DailyRoutineItemDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<List<DailyRoutineItemDto>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyDailyRoutine([FromQuery] DateTime? date = null)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<List<DailyRoutineItemDto>>.Fail("No se pudo identificar al usuario autenticado."));
        }

        var result = await _studentService.GetDailyRoutineForStudentUserAsync(userId.Value, date);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Obtiene los datos de perfil del estudiante autenticado.
    /// </summary>
    [HttpGet("me/profile")]
    [Authorize(Roles = "Estudiante")]
    [ProducesResponseType(typeof(ApiResponse<StudentProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StudentProfileDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyProfile()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<StudentProfileDto>.Fail("No se pudo identificar al usuario autenticado."));
        }

        var result = await _studentService.GetStudentProfileByUserIdAsync(userId.Value);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Consulta los estudiantes activos (Docente y Administrador ven todos; Acudiente ve solo sus asignados).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Docente,Administrador,Acudiente")]
    [ProducesResponseType(typeof(ApiResponse<List<StudentProfileDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetCurrentUserId();
        if (User.IsInRole("Acudiente") && userId.HasValue)
        {
            var result = await _studentService.GetStudentsForGuardianUserAsync(userId.Value);
            return Ok(result);
        }

        var allResult = await _studentService.GetAllStudentsAsync();
        return Ok(allResult);
    }

    /// <summary>
    /// Consulta los logros, insignias personales y módulos concluidos del estudiante autenticado (HU-011).
    /// </summary>
    [HttpGet("me/achievements")]
    [Authorize(Roles = "Estudiante")]
    [ProducesResponseType(typeof(ApiResponse<StudentAchievementsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StudentAchievementsDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<StudentAchievementsDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyAchievements(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<StudentAchievementsDto>.Fail("No se pudo identificar al usuario autenticado."));
        }

        var result = await _achievementService.GetStudentAchievementsByUserIdAsync(userId.Value, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Consulta las actividades asignadas al estudiante para fines de bitácora y calificaciones (HU-010).
    /// </summary>
    [HttpGet("{studentId:guid}/grades")]
    [Authorize(Roles = "Docente,Administrador")]
    public async Task<IActionResult> GetStudentGrades([FromRoute] Guid studentId, CancellationToken cancellationToken)
    {
        var assignments = await _assignmentRepository.GetFilteredAsync(studentProfileId: studentId, cancellationToken: cancellationToken);
        var result = assignments.Select(a => new
        {
            AssignmentId = a.Id,
            Titulo = a.Activity?.Titulo ?? "Actividad pedagógica",
            TipoActividad = a.Activity?.TipoActividad.ToString() ?? "Actividad",
            Calificacion = a.Estado == EstadoAsignacion.Completada ? (double?)4.5 : null,
            Comentario = a.Estado == EstadoAsignacion.Completada ? "Completada con éxito" : null,
            FechaCalificacion = a.UpdatedAt?.ToString("yyyy-MM-ddTHH:mm:ss") ?? a.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            PuedeEditar = true
        }).ToList();

        return Ok(ApiResponse<object>.Ok(result, "Calificaciones recuperadas exitosamente."));
    }
}

