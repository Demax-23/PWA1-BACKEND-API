using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Pedagogical;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Api.Controllers;

[Authorize]
[Route("api/students/{id:guid}/pedagogical-notes")]
public class PedagogicalNotesController : BaseApiController
{
    private readonly IPedagogicalNoteService _noteService;

    public PedagogicalNotesController(IPedagogicalNoteService noteService)
    {
        _noteService = noteService;
    }

    /// <summary>
    /// Registra una nueva observación o nota cualitativa en la bitácora pedagógica del estudiante (HU-010).
    /// Restringido a Docente y Administrador.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Docente,Administrador")]
    [ProducesResponseType(typeof(ApiResponse<PedagogicalNoteDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<PedagogicalNoteDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PedagogicalNoteDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<PedagogicalNoteDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateNote(
        [FromRoute] Guid id, 
        [FromBody] CreatePedagogicalNoteDto dto, 
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse<PedagogicalNoteDto>.Fail(string.Join("; ", errors)));
        }

        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized(ApiResponse<PedagogicalNoteDto>.Fail("No se pudo identificar al usuario docente autenticado."));
        }

        var result = await _noteService.CreateNoteAsync(id, currentUserId.Value, dto, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Consulta cronológica de las notas cualitativas de la bitácora pedagógica del estudiante (HU-010).
    /// Accesible para Docentes, Administradores y el Acudiente asignado al estudiante.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Docente,Administrador,Acudiente")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PedagogicalNoteDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PedagogicalNoteDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PedagogicalNoteDto>>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PedagogicalNoteDto>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNotes([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized(ApiResponse<IReadOnlyList<PedagogicalNoteDto>>.Fail("No se pudo identificar al usuario autenticado."));
        }

        bool isStaff = User.IsInRole("Docente") || User.IsInRole("Administrador");
        var result = await _noteService.GetNotesByStudentIdAsync(id, currentUserId.Value, isStaff, cancellationToken);
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
