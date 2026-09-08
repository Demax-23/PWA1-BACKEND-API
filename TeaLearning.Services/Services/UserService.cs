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

    public UserService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
    }

    public async Task<ApiResponse<List<UserDto>>> GetAllUsersAsync()
    {
        var users = await _userManager.Users
            .Include(u => u.StudentProfile)
            .Include(u => u.TeacherProfile)
            .Include(u => u.GuardianProfile)
            .OrderBy(u => u.Nombres)
            .ToListAsync();

        var dtoList = new List<UserDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
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
                EstudianteId = user.StudentProfile?.Id,
                DocenteId = user.TeacherProfile?.Id,
                AcudienteId = user.GuardianProfile?.Id
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
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return ApiResponse<UserDto>.Fail("Usuario no encontrado.");
        }

        var roles = await _userManager.GetRolesAsync(user);

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
            EstudianteId = user.StudentProfile?.Id,
            DocenteId = user.TeacherProfile?.Id,
            AcudienteId = user.GuardianProfile?.Id
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
            EstudianteId = studentProfile?.Id,
            DocenteId = teacherProfile?.Id,
            AcudienteId = guardianProfile?.Id
        };

        return ApiResponse<UserDto>.Ok(updatedDto, "Roles de usuario actualizados exitosamente");
    }
}
