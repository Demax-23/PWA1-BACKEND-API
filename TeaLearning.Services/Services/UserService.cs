using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Data.Context;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Users;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Services.Services;

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;

    public UserService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        ApplicationDbContext context,
        IFileStorageService fileStorageService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _fileStorageService = fileStorageService;
    }

    public async Task<ApiResponse<List<UserDto>>> GetAllUsersAsync()
    {
        var users = await _userManager.Users
            .Include(u => u.StudentProfile)
            .Include(u => u.TeacherProfile)
            .Include(u => u.GuardianProfile)
                .ThenInclude(g => g!.Students)
                    .ThenInclude(s => s.User)
            .OrderBy(u => u.Nombres)
            .ToListAsync();

        var dtoList = new List<UserDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var studentUserIds = user.GuardianProfile?.Students
                .Where(s => s.IsActive)
                .Select(s => s.UserId)
                .ToList() ?? new List<Guid>();

            var studentNombres = user.GuardianProfile?.Students
                .Where(s => s.IsActive)
                .Select(s => s.User?.NombreCompleto ?? string.Empty)
                .ToList() ?? new List<string>();

            dtoList.Add(new UserDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                Nombres = user.Nombres,
                Apellidos = user.Apellidos,
                NombreCompleto = user.NombreCompleto,
                Roles = roles.ToList(),
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                UltimaActividadAt = user.UltimaActividadAt,
                AvatarUrl = user.AvatarUrl,
                EstudianteId = user.StudentProfile?.Id,
                DocenteId = user.TeacherProfile?.Id,
                AcudienteId = user.GuardianProfile?.Id,
                StudentUserIds = studentUserIds,
                StudentNombres = studentNombres
            });
        }

        return ApiResponse<List<UserDto>>.Ok(dtoList, "Lista de usuarios recuperada exitosamente");
    }

    public async Task<ApiResponse<UserDto>> GetUserByIdAsync(Guid id)
    {
        var user = await _userManager.Users
            .Include(u => u.StudentProfile)
            .Include(u => u.TeacherProfile)
            .Include(u => u.GuardianProfile)
                .ThenInclude(g => g!.Students)
                    .ThenInclude(s => s.User)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return ApiResponse<UserDto>.Fail("Usuario no encontrado.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var studentUserIds = user.GuardianProfile?.Students
            .Where(s => s.IsActive)
            .Select(s => s.UserId)
            .ToList() ?? new List<Guid>();

        var studentNombres = user.GuardianProfile?.Students
            .Where(s => s.IsActive)
            .Select(s => s.User?.NombreCompleto ?? string.Empty)
            .ToList() ?? new List<string>();

        var dto = new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            Nombres = user.Nombres,
            Apellidos = user.Apellidos,
            NombreCompleto = user.NombreCompleto,
            Roles = roles.ToList(),
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UltimaActividadAt = user.UltimaActividadAt,
            AvatarUrl = user.AvatarUrl,
            EstudianteId = user.StudentProfile?.Id,
            DocenteId = user.TeacherProfile?.Id,
            AcudienteId = user.GuardianProfile?.Id,
            StudentUserIds = studentUserIds,
            StudentNombres = studentNombres
        };

        return ApiResponse<UserDto>.Ok(dto, "Usuario recuperado exitosamente");
    }

    public async Task<ApiResponse<UserDto>> UpdateUserRolesAsync(Guid id, UpdateUserRolesDto dto)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return ApiResponse<UserDto>.Fail("Usuario no encontrado.");
        }

        if (dto.Roles == null || !dto.Roles.Any())
        {
            return ApiResponse<UserDto>.Fail("Debe especificar al menos un rol para el usuario.");
        }

        // Validar que los roles solicitados existan
        foreach (var roleName in dto.Roles)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                return ApiResponse<UserDto>.Fail($"El rol '{roleName}' no existe en el sistema.");
            }
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!removeResult.Succeeded)
        {
            return ApiResponse<UserDto>.Fail("Error al remover roles actuales", removeResult.Errors.Select(e => e.Description).ToList());
        }

        var addResult = await _userManager.AddToRolesAsync(user, dto.Roles);
        if (!addResult.Succeeded)
        {
            return ApiResponse<UserDto>.Fail("Error al asignar nuevos roles", addResult.Errors.Select(e => e.Description).ToList());
        }

        var studentProfile = await _context.StudentProfiles.FirstOrDefaultAsync(s => s.UserId == user.Id);
        var teacherProfile = await _context.TeacherProfiles.FirstOrDefaultAsync(t => t.UserId == user.Id);
        var guardianProfile = await _context.GuardianProfiles.FirstOrDefaultAsync(g => g.UserId == user.Id);

        var updatedDto = new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            Nombres = user.Nombres,
            Apellidos = user.Apellidos,
            NombreCompleto = user.NombreCompleto,
            Roles = dto.Roles,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UltimaActividadAt = user.UltimaActividadAt,
            AvatarUrl = user.AvatarUrl,
            EstudianteId = studentProfile?.Id,
            DocenteId = teacherProfile?.Id,
            AcudienteId = guardianProfile?.Id
        };

        return ApiResponse<UserDto>.Ok(updatedDto, "Roles de usuario actualizados exitosamente");
    }

    public async Task<ApiResponse<UserDto>> UpdateUserAsync(Guid id, UpdateUserDto dto)
    {
        var user = await _userManager.Users
            .Include(u => u.StudentProfile)
            .Include(u => u.TeacherProfile)
            .Include(u => u.GuardianProfile)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return ApiResponse<UserDto>.Fail("Usuario no encontrado.");
        }

        if (!string.IsNullOrWhiteSpace(dto.Nombres))
        {
            user.Nombres = dto.Nombres.Trim();
        }
        if (!string.IsNullOrWhiteSpace(dto.Apellidos))
        {
            user.Apellidos = dto.Apellidos.Trim();
        }
        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            if (!string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            {
                var existing = await _userManager.FindByEmailAsync(normalizedEmail);
                if (existing != null && existing.Id != id)
                {
                    return ApiResponse<UserDto>.Fail("Ya existe un usuario registrado con ese correo electrónico.");
                }
                user.Email = normalizedEmail;
                user.NormalizedEmail = _userManager.NormalizeEmail(normalizedEmail);
                user.UserName = normalizedEmail;
                user.NormalizedUserName = _userManager.NormalizeName(normalizedEmail);
            }
        }
        if (dto.IsActive.HasValue)
        {
            user.IsActive = dto.IsActive.Value;
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return ApiResponse<UserDto>.Fail("Error al actualizar información básica del usuario.", updateResult.Errors.Select(e => e.Description).ToList());
        }

        if (dto.Roles != null && dto.Roles.Any())
        {
            foreach (var roleName in dto.Roles)
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    return ApiResponse<UserDto>.Fail($"El rol '{roleName}' no existe en el sistema.");
                }
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                return ApiResponse<UserDto>.Fail("Error al actualizar roles previos", removeResult.Errors.Select(e => e.Description).ToList());
            }

            var addResult = await _userManager.AddToRolesAsync(user, dto.Roles);
            if (!addResult.Succeeded)
            {
                return ApiResponse<UserDto>.Fail("Error al asignar los nuevos roles", addResult.Errors.Select(e => e.Description).ToList());
            }
        }

        var finalRoles = await _userManager.GetRolesAsync(user);

        var studentProfile = await _context.StudentProfiles.FirstOrDefaultAsync(s => s.UserId == user.Id);
        var teacherProfile = await _context.TeacherProfiles.FirstOrDefaultAsync(t => t.UserId == user.Id);
        var guardianProfile = await _context.GuardianProfiles.FirstOrDefaultAsync(g => g.UserId == user.Id);

        var updatedDto = new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            Nombres = user.Nombres,
            Apellidos = user.Apellidos,
            NombreCompleto = user.NombreCompleto,
            Roles = finalRoles.ToList(),
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UltimaActividadAt = user.UltimaActividadAt,
            AvatarUrl = user.AvatarUrl,
            EstudianteId = studentProfile?.Id,
            DocenteId = teacherProfile?.Id,
            AcudienteId = guardianProfile?.Id
        };

        return ApiResponse<UserDto>.Ok(updatedDto, "Usuario actualizado exitosamente");
    }

    public async Task<ApiResponse<bool>> DeleteUserAsync(Guid id)
    {
        var user = await _userManager.Users
            .Include(u => u.StudentProfile)
            .Include(u => u.TeacherProfile)
            .Include(u => u.GuardianProfile)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return ApiResponse<bool>.Fail("Usuario no encontrado en la base de datos.");
        }

        // Si es Acudiente, desvincular estudiantes asociados para evitar violación de restricción referencial
        if (user.GuardianProfile != null)
        {
            var linkedStudents = await _context.StudentProfiles
                .Where(s => s.GuardianProfileId == user.GuardianProfile.Id)
                .ToListAsync();
            foreach (var s in linkedStudents)
            {
                s.GuardianProfileId = null;
            }
            await _context.SaveChangesAsync();
        }

        // Si es Docente, verificar si tiene aulas o actividades restringidas
        if (user.TeacherProfile != null)
        {
            var hasActivities = await _context.Activities.AnyAsync(a => a.DocenteId == user.TeacherProfile.Id);
            var hasClassrooms = await _context.Classrooms.AnyAsync(c => c.TeacherProfileId == user.TeacherProfile.Id);
            if (hasActivities || hasClassrooms)
            {
                return ApiResponse<bool>.Fail("No es posible eliminar el docente porque tiene actividades pedagógicas o aulas vinculadas. Debe reasignar o eliminar esas actividades primero.");
            }
        }

        var deleteResult = await _userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
        {
            return ApiResponse<bool>.Fail("Error al eliminar usuario de la base de datos.", deleteResult.Errors.Select(e => e.Description).ToList());
        }

        return ApiResponse<bool>.Ok(true, "Usuario eliminado permanentemente de la base de datos.");
    }

    public async Task<ApiResponse<List<Guid>>> LinkStudentsToGuardianAsync(Guid guardianUserId, List<Guid> studentIds)
    {
        var user = await _userManager.FindByIdAsync(guardianUserId.ToString());
        if (user == null)
        {
            return ApiResponse<List<Guid>>.Fail("Usuario no encontrado.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains("Acudiente"))
        {
            return ApiResponse<List<Guid>>.Fail("El usuario especificado no cuenta con el rol de Acudiente.");
        }

        var guardian = await _context.GuardianProfiles
            .Include(g => g.Students)
            .FirstOrDefaultAsync(g => g.UserId == guardianUserId);

        if (guardian == null)
        {
            guardian = new GuardianProfile
            {
                UserId = user.Id,
                TelefonoContacto = "Sin especificar",
                Parentesco = "Familiar"
            };
            _context.GuardianProfiles.Add(guardian);
            await _context.SaveChangesAsync();
        }

        // 1. Desvincular estudiantes actuales que no estén en la nueva lista
        var currentStudents = await _context.StudentProfiles
            .Where(s => s.GuardianProfileId == guardian.Id)
            .ToListAsync();

        foreach (var cs in currentStudents)
        {
            if (!studentIds.Contains(cs.Id) && !studentIds.Contains(cs.UserId))
            {
                cs.GuardianProfileId = null;
            }
        }

        // 2. Vincular los nuevos estudiantes solicitados
        var targetStudents = new List<StudentProfile>();
        if (studentIds.Any())
        {
            targetStudents = await _context.StudentProfiles
                .Where(s => studentIds.Contains(s.Id) || studentIds.Contains(s.UserId))
                .ToListAsync();

            foreach (var ts in targetStudents)
            {
                ts.GuardianProfileId = guardian.Id;
            }
        }

        await _context.SaveChangesAsync();

        var linkedUserIds = targetStudents.Select(s => s.UserId).ToList();
        return ApiResponse<List<Guid>>.Ok(linkedUserIds, $"Se vincularon exitosamente {linkedUserIds.Count} estudiantes al acudiente.");
    }

    public async Task<ApiResponse<bool>> ResetStudentPasswordAsync(Guid guardianUserId, Guid studentId, string newPassword)
    {
        // El identificador puede ser el de StudentProfile o el del usuario, igual que en LinkStudentsToGuardianAsync.
        var student = await _context.StudentProfiles
            .Include(s => s.User)
            .Include(s => s.GuardianProfile)
            .FirstOrDefaultAsync(s => s.Id == studentId || s.UserId == studentId);

        if (student == null || student.User == null)
        {
            return ApiResponse<bool>.Fail("Estudiante no encontrado.");
        }

        // Solo el acudiente titular puede cambiar la contraseña de su estudiante.
        if (student.GuardianProfile == null || student.GuardianProfile.UserId != guardianUserId)
        {
            return ApiResponse<bool>.Fail("No tiene autorización para modificar la contraseña de este estudiante.");
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(student.User);
        var result = await _userManager.ResetPasswordAsync(student.User, token, newPassword);
        if (!result.Succeeded)
        {
            return ApiResponse<bool>.Fail(
                "No fue posible actualizar la contraseña del estudiante.",
                IdentityErrorTranslator.Translate(result.Errors));
        }

        // Cierra las sesiones renovables previas del estudiante.
        student.User.RefreshToken = null;
        student.User.RefreshTokenExpiryTime = null;
        await _userManager.UpdateAsync(student.User);

        return ApiResponse<bool>.Ok(true, "La contraseña del estudiante fue actualizada exitosamente.");
    }

    public async Task<ApiResponse<UserDto>> GetCurrentUserProfileAsync(Guid userId)
    {
        return await GetUserByIdAsync(userId);
    }

    public async Task<ApiResponse<UserDto>> UpdateAvatarAsync(Guid userId, Stream fileStream, string originalFileName, string contentType, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return ApiResponse<UserDto>.Fail("Usuario no encontrado.");
        }

        // Si ya tenía avatar anterior, eliminar el archivo previo
        if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
        {
            await _fileStorageService.DeleteFileAsync(user.AvatarUrl, cancellationToken);
        }

        string avatarRelativeUrl;
        try
        {
            avatarRelativeUrl = await _fileStorageService.SaveAvatarAsync(userId, fileStream, originalFileName, contentType, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return ApiResponse<UserDto>.Fail(ex.Message);
        }

        user.AvatarUrl = avatarRelativeUrl;
        user.UltimaActividadAt = DateTime.UtcNow;
        var updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            return ApiResponse<UserDto>.Fail("Error al guardar la foto de perfil en la base de datos.");
        }

        return await GetUserByIdAsync(userId);
    }

    public async Task<ApiResponse<bool>> RemoveAvatarAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return ApiResponse<bool>.Fail("Usuario no encontrado.");
        }

        if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
        {
            await _fileStorageService.DeleteFileAsync(user.AvatarUrl, cancellationToken);
            user.AvatarUrl = null;
            user.UltimaActividadAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
        }

        return ApiResponse<bool>.Ok(true, "Foto de perfil eliminada exitosamente.");
    }
}
