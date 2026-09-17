using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeaLearning.Core.Enums;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Activities;
using TeaLearning.Services.DTOs.Execution;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Api.Controllers;

[Authorize]
public class ActivitiesController : BaseApiController
{
    private readonly IActivityService _activityService;
    private readonly IActivityExecutionService _executionService;

    public ActivitiesController(
        IActivityService activityService,
        IActivityExecutionService executionService)
    {
        _activityService = activityService;
        _executionService = executionService;
    }

    /// <summary>
    /// Consulta actividades pedagógicas con filtros opcionales (HU-004).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ActivityDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? moduloId = null, 
        [FromQuery] TipoActividad? tipoActividad = null, 
        [FromQuery] Guid? docenteId = null)
    {
        var result = await _activityService.GetActivitiesAsync(moduloId, tipoActividad, docenteId);
        return Ok(result);
    }

    /// <summary>
    /// Consulta el detalle de una actividad incluyendo su estructura dinámica ConfigJson (HU-004).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ActivityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ActivityDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _activityService.GetActivityByIdAsync(id);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Crea y parametriza una nueva actividad pedagógica con modelo híbrido y ConfigJson (HU-004).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Docente,Administrador")]
    [ProducesResponseType(typeof(ApiResponse<ActivityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ActivityDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ActivityDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateActivityDto dto)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<ActivityDto>.Fail("No se pudo identificar al usuario autenticado."));
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<ActivityDto>.Fail("Datos de actividad inválidos", errors));
        }

        var result = await _activityService.CreateActivityAsync(userId.Value, dto);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Actualiza la información o parametrización dinámica ConfigJson de una actividad (HU-004).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Docente,Administrador")]
    [ProducesResponseType(typeof(ApiResponse<ActivityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ActivityDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ActivityDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateActivityDto dto)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<ActivityDto>.Fail("No se pudo identificar al usuario autenticado."));
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<ActivityDto>.Fail("Datos de actualización inválidos", errors));
        }

        bool isAdmin = User.IsInRole("Administrador");
        var result = await _activityService.UpdateActivityAsync(id, userId.Value, dto, isAdmin);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Elimina lógicamente una actividad pedagógica (HU-004).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Docente,Administrador")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<bool>.Fail("No se pudo identificar al usuario autenticado."));
        }

        bool isAdmin = User.IsInRole("Administrador");
        var result = await _activityService.DeleteActivityAsync(id, userId.Value, isAdmin);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Inicia la ejecución de una actividad asignada, cambiando el estado a EnProgreso y retornando el perfil sensorial adaptativo (HU-006).
    /// </summary>
    [HttpPost("{assignmentId:guid}/start")]
    [Authorize(Roles = "Estudiante")]
    [ProducesResponseType(typeof(ApiResponse<StartActivityResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StartActivityResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<StartActivityResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartActivity(Guid assignmentId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<StartActivityResponseDto>.Fail("No se pudo identificar al estudiante autenticado."));
        }

        var result = await _executionService.StartActivityAsync(assignmentId, userId.Value);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Registra la telemetría de ejecución de una actividad (tiempo, aciertos, intentos fallidos) y actualiza el estado de la asignación (HU-006).
    /// </summary>
    [HttpPost("{assignmentId:guid}/submit")]
    [Authorize(Roles = "Estudiante")]
    [ProducesResponseType(typeof(ApiResponse<ExecutionResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ExecutionResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ExecutionResultDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitActivity(Guid assignmentId, [FromBody] SubmitActivityDto dto)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<ExecutionResultDto>.Fail("No se pudo identificar al estudiante autenticado."));
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<ExecutionResultDto>.Fail("Datos de telemetría inválidos", errors));
        }

        var result = await _executionService.SubmitActivityAsync(assignmentId, userId.Value, dto);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
