using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Sensory;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Api.Controllers;

[Authorize(Roles = "Acudiente,Administrador")]
[Route("api/students/{id:guid}/sensory-preferences")]
public class SensoryPreferencesController : BaseApiController
{
    private readonly ISensoryPreferenceService _sensoryService;

    public SensoryPreferencesController(ISensoryPreferenceService sensoryService)
    {
        _sensoryService = sensoryService;
    }

    /// <summary>
    /// Consulta las preferencias sensoriales adaptativas de un estudiante (Restringido a Acudiente o Administrador - HU-003).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<SensoryPreferenceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SensoryPreferenceDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPreferences(Guid id)
    {
        var result = await _sensoryService.GetSensoryPreferencesByStudentIdAsync(id);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Actualiza la configuración sensorial adaptativa (paleta, tipografía, audio, animaciones, contraste, etc.) (HU-003).
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<SensoryPreferenceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SensoryPreferenceDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<SensoryPreferenceDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePreferences(Guid id, [FromBody] UpdateSensoryPreferenceDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<SensoryPreferenceDto>.Fail("Datos sensoriales inválidos", errors));
        }

        var result = await _sensoryService.UpdateSensoryPreferencesAsync(id, dto);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
