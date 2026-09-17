using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Modules;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Api.Controllers;

[Authorize]
public class ModulesController : BaseApiController
{
    private readonly IModuleService _moduleService;

    public ModulesController(IModuleService moduleService)
    {
        _moduleService = moduleService;
    }

    /// <summary>
    /// Consulta todos los módulos temáticos activos con el conteo de actividades (HU-007).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ModuleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool onlyActive = true)
    {
        var result = await _moduleService.GetAllModulesAsync(onlyActive);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene el detalle de un módulo temático con la lista de actividades asociadas (HU-007).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ModuleDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ModuleDetailDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _moduleService.GetModuleByIdAsync(id);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Crea un nuevo módulo temático de aprendizaje (Restringido a Administrador - HU-007).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(ApiResponse<ModuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ModuleDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateModuleDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<ModuleDto>.Fail("Datos de creación del módulo inválidos", errors));
        }

        var result = await _moduleService.CreateModuleAsync(dto);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Actualiza un módulo temático existente (Restringido a Administrador - HU-007).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(ApiResponse<ModuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ModuleDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ModuleDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateModuleDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<ModuleDto>.Fail("Datos de actualización inválidos", errors));
        }

        var result = await _moduleService.UpdateModuleAsync(id, dto);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Desactiva (eliminación lógica) un módulo temático (Restringido a Administrador - HU-007).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _moduleService.DeleteModuleAsync(id);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }
}
