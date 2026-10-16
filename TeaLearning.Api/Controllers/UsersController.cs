using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Users;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Api.Controllers;

[Authorize]
public class UsersController : BaseApiController
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Obtiene la lista completa de usuarios registrados y sus roles asignados (Solo Administrador).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(ApiResponse<List<UserDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<UserDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<List<UserDto>>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _userService.GetAllUsersAsync();
        return Ok(result);
    }

    /// <summary>
    /// Obtiene el detalle de un usuario por su identificador único (Solo Administrador).
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _userService.GetUserByIdAsync(id);
        if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Actualiza los roles RBAC asignados a un usuario específico (Solo Administrador).
    /// </summary>
    [HttpPut("{id:guid}/roles")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRoles(Guid id, [FromBody] UpdateUserRolesDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<UserDto>.Fail("Datos de roles inválidos"));
        }

        var currentUserId = GetCurrentUserId();
        if (currentUserId.HasValue && currentUserId.Value == id && !dto.Roles.Contains("Administrador", StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse<UserDto>.Fail("No es posible remover el rol de Administrador de la cuenta actualmente en sesión."));
        }

        var result = await _userService.UpdateUserRolesAsync(id, dto);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Actualiza los datos generales, estado y roles de un usuario (Solo Administrador).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<UserDto>.Fail("Datos de usuario inválidos"));
        }

        var currentUserId = GetCurrentUserId();
        if (currentUserId.HasValue && currentUserId.Value == id)
        {
            if (dto.IsActive.HasValue && !dto.IsActive.Value)
            {
                return BadRequest(ApiResponse<UserDto>.Fail("No es posible desactivar la cuenta del administrador que se encuentra actualmente en sesión."));
            }

            if (dto.Roles != null && dto.Roles.Any() && !dto.Roles.Contains("Administrador", StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(ApiResponse<UserDto>.Fail("No es posible remover el rol de Administrador de la cuenta actualmente en sesión."));
            }
        }

        var result = await _userService.UpdateUserAsync(id, dto);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Elimina permanentemente un usuario del sistema (Solo Administrador).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId.HasValue && currentUserId.Value == id)
        {
            return BadRequest(ApiResponse<bool>.Fail("No es posible eliminar la cuenta del administrador que se encuentra actualmente en sesión."));
        }

        var result = await _userService.DeleteUserAsync(id);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Vincula o desvincula estudiantes a cargo de un acudiente (Solo Administrador).
    /// </summary>
    [HttpPut("{id:guid}/students")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(ApiResponse<List<Guid>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<Guid>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<List<Guid>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LinkStudents(Guid id, [FromBody] LinkStudentsDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<List<Guid>>.Fail("Datos de vinculación inválidos", errors));
        }

        var result = await _userService.LinkStudentsToGuardianAsync(id, dto.StudentIds);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Obtiene los datos de perfil y avatar del usuario actualmente autenticado (Cualquier rol).
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUserProfile()
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized(ApiResponse<UserDto>.Fail("No se pudo identificar al usuario autenticado."));
        }

        var result = await _userService.GetCurrentUserProfileAsync(currentUserId.Value);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Sube o actualiza la foto de perfil personalizada (avatar) del usuario actualmente autenticado (Cualquier rol).
    /// </summary>
    [HttpPost("me/avatar")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadAvatar(IFormFile file, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized(ApiResponse<UserDto>.Fail("No se pudo identificar al usuario autenticado."));
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<UserDto>.Fail("Debe seleccionar un archivo de imagen válido."));
        }

        // Límite de tamaño: 5 MB
        if (file.Length > 5 * 1024 * 1024)
        {
            return BadRequest(ApiResponse<UserDto>.Fail("El tamaño de la imagen no puede superar los 5 MB."));
        }

        using var stream = file.OpenReadStream();
        var result = await _userService.UpdateAvatarAsync(currentUserId.Value, stream, file.FileName, file.ContentType, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Elimina la foto de perfil personalizada del usuario actual, volviendo al distintivo por defecto (Cualquier rol).
    /// </summary>
    [HttpDelete("me/avatar")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveAvatar(CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized(ApiResponse<bool>.Fail("No se pudo identificar al usuario autenticado."));
        }

        var result = await _userService.RemoveAvatarAsync(currentUserId.Value, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
