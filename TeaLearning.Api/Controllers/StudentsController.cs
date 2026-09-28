using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public StudentsController(
        IStudentService studentService,
        IAchievementService achievementService)
    {
        _studentService = studentService;
        _achievementService = achievementService;
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
    /// Consulta todos los estudiantes activos (Restringido a Docente y Administrador para asignación pedagógica).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Docente,Administrador")]
    [ProducesResponseType(typeof(ApiResponse<List<StudentProfileDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _studentService.GetAllStudentsAsync();
        return Ok(result);
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
}

