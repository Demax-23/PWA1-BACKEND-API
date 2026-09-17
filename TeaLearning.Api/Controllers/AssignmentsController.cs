using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeaLearning.Core.Enums;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Assignments;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Api.Controllers;

[Authorize]
public class AssignmentsController : BaseApiController
{
    private readonly IAssignmentService _assignmentService;

    public AssignmentsController(IAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    /// <summary>
    /// Asigna una actividad individualmente a un estudiante o en lote a toda un aula (HU-005).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Docente,Administrador")]
    [ProducesResponseType(typeof(ApiResponse<List<AssignmentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<AssignmentDto>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateAssignmentDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<List<AssignmentDto>>.Fail("Datos de asignación inválidos", errors));
        }

        var result = await _assignmentService.CreateAssignmentAsync(dto);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Consulta asignaciones de actividades con filtros por estudiante, aula o estado (HU-005).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Docente,Administrador,Acudiente")]
    [ProducesResponseType(typeof(ApiResponse<List<AssignmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? studentProfileId = null,
        [FromQuery] Guid? classroomId = null,
        [FromQuery] EstadoAsignacion? estado = null)
    {
        var result = await _assignmentService.GetAssignmentsAsync(studentProfileId, classroomId, estado);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene el detalle de una asignación específica (HU-005).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AssignmentDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _assignmentService.GetAssignmentByIdAsync(id);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Elimina o cancela una asignación de actividad (HU-005).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Docente,Administrador")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _assignmentService.DeleteAssignmentAsync(id);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }
}
