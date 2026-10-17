using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Sensory;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Api.Controllers;

[Authorize(Roles = "Acudiente,Administrador,Estudiante")]
[Route("api/students/{id:guid}/sensory-preferences")]
public class SensoryPreferencesController : BaseApiController
{
    private readonly ISensoryPreferenceService _sensoryService;

    public SensoryPreferencesController(ISensoryPreferenceService sensoryService)
    {
        _sensoryService = sensoryService;
    }

    /// <summary>
    /// Consulta las preferencias sensoriales adaptativas de un estudiante (HU-003).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<SensoryPreferenceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SensoryPreferenceDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<SensoryPreferenceDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPreferences(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<SensoryPreferenceDto>.Fail("No se pudo identificar al usuario autenticado."));
        }

        bool isStaff = User.IsInRole("Administrador") || User.IsInRole("Docente");
        var result = await _sensoryService.GetSensoryPreferencesByStudentIdAsync(id, userId.Value, isStaff);
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
    /// Actualiza la configuración sensorial adaptativa (paleta, tipografía, audio, animaciones, contraste, etc.) (Restringido a Acudiente titular o Administrador - HU-003).
    /// </summary>
    [HttpPut]
    [Authorize(Roles = "Acudiente,Administrador")]
    [ProducesResponseType(typeof(ApiResponse<SensoryPreferenceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SensoryPreferenceDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<SensoryPreferenceDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<SensoryPreferenceDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePreferences(Guid id, [FromBody] UpdateSensoryPreferenceDto dto)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<SensoryPreferenceDto>.Fail("No se pudo identificar al usuario autenticado."));
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<SensoryPreferenceDto>.Fail("Datos sensoriales inválidos", errors));
        }

        bool isAdmin = User.IsInRole("Administrador");
        var result = await _sensoryService.UpdateSensoryPreferencesAsync(id, dto, userId.Value, isAdmin);
        if (!result.Success)
        {
            if (result.Message != null && result.Message.Contains("autorización", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, result);
            }
            if (result.Message != null && result.Message.Contains("No se encontró", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(result);
            }
            return BadRequest(result);
        }

        return Ok(result);
    }
}
