using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Data.Context;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Auth;
using TeaLearning.Services.DTOs.Users;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Services.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IConfiguration _configuration;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        ApplicationDbContext context,
        IJwtService jwtService,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _jwtService = jwtService;
        _configuration = configuration;
    }

    public async Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterRequestDto dto)
    {
        // 1. Validar que el rol sea válido
        string normalizedRole = dto.Rol.Trim();
        string[] validRoles = ["Administrador", "Docente", "Estudiante", "Acudiente"];
        var matchedRole = validRoles.FirstOrDefault(r => r.Equals(normalizedRole, StringComparison.OrdinalIgnoreCase));
        if (matchedRole == null)
        {
            return ApiResponse<AuthResponseDto>.Fail($"El rol '{dto.Rol}' no es válido. Roles permitidos: Administrador, Docente, Estudiante, Acudiente.");
        }

        // 2. Validar que el usuario no exista
        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
        {
            return ApiResponse<AuthResponseDto>.Fail("Ya existe un usuario registrado con este correo electrónico.");
        }

        // 3. Asegurar que el rol exista en el RoleManager
        if (!await _roleManager.RoleExistsAsync(matchedRole))
        {
            await _roleManager.CreateAsync(new IdentityRole<Guid>(matchedRole));
        }

        // 4. Crear el usuario
        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            Nombres = dto.Nombres.Trim(),
            Apellidos = dto.Apellidos.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, dto.Password);
        if (!createResult.Succeeded)
        {
            var errors = createResult.Errors.Select(e => e.Description).ToList();
            return ApiResponse<AuthResponseDto>.Fail("No fue posible registrar el usuario", errors);
        }

        await _userManager.AddToRoleAsync(user, matchedRole);

        Guid? studentId = null;
        Guid? teacherId = null;
        Guid? guardianId = null;

        // 5. Crear el perfil de rol correspondiente
        if (matchedRole == "Estudiante")
        {
            var studentProfile = new StudentProfile
            {
                UserId = user.Id,
                CodigoEstudiante = !string.IsNullOrWhiteSpace(dto.CodigoEstudiante) 
                    ? dto.CodigoEstudiante 
                    : $"EST-{DateTime.UtcNow.Ticks % 100000:D5}",
                FechaNacimiento = dto.FechaNacimiento ?? DateTime.UtcNow.AddYears(-10),
                ClassroomId = dto.ClassroomId,
                GuardianProfileId = dto.GuardianProfileId
            };
            _context.StudentProfiles.Add(studentProfile);
            await _context.SaveChangesAsync();

            studentId = studentProfile.Id;

            // Inicializar preferencias sensoriales adaptativas por defecto
            var sensoryPreference = new SensoryPreference
            {
                StudentProfileId = studentProfile.Id,
                PaletaColor = "PastelSuave",
                Tipografia = "OpenDyslexic",
                AudioActivo = true,
                AnimacionesReducidas = true,
                NivelEscalaFuente = 2,
                ModoContraste = ModoContraste.Estandar,
                OcultarTemporizadores = true
            };
            _context.SensoryPreferences.Add(sensoryPreference);
            await _context.SaveChangesAsync();
        }
        else if (matchedRole == "Docente")
        {
            var teacherProfile = new TeacherProfile
            {
                UserId = user.Id,
                Especialidad = dto.Especialidad ?? "Pedagogía y Educación Especial"
            };
            _context.TeacherProfiles.Add(teacherProfile);
            await _context.SaveChangesAsync();
            teacherId = teacherProfile.Id;
        }
        else if (matchedRole == "Acudiente")
        {
            var guardianProfile = new GuardianProfile
            {
                UserId = user.Id,
                TelefonoContacto = dto.Telefono ?? "Sin especificar",
                Parentesco = dto.Parentesco ?? "Familiar"
            };
            _context.GuardianProfiles.Add(guardianProfile);
            await _context.SaveChangesAsync();
            guardianId = guardianProfile.Id;
        }

        // 6. Generar JWT y Refresh Token
        var roles = new List<string> { matchedRole };
        var (token, expiration) = _jwtService.GenerateAccessToken(user, roles, studentId, teacherId, guardianId);
        var refreshToken = _jwtService.GenerateRefreshToken();

        int refreshDurationDays = int.TryParse(_configuration["Jwt:RefreshTokenDurationInDays"], out var days) ? days : 7;
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(refreshDurationDays);
        await _userManager.UpdateAsync(user);

        var authResponse = new AuthResponseDto
        {
            Token = token,
            RefreshToken = refreshToken,
            Expiration = expiration,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                Nombres = user.Nombres,
                Apellidos = user.Apellidos,
                NombreCompleto = user.NombreCompleto,
                Roles = roles,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                EstudianteId = studentId,
                DocenteId = teacherId,
                AcudienteId = guardianId
            }
        };

        return ApiResponse<AuthResponseDto>.Ok(authResponse, "Usuario registrado exitosamente");
    }

    public async Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginRequestDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
        {
            return ApiResponse<AuthResponseDto>.Fail("Credenciales inválidas.");
        }

        if (!user.IsActive)
        {
            return ApiResponse<AuthResponseDto>.Fail("La cuenta de usuario se encuentra inactiva. Contacte al administrador.");
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
        if (!isPasswordValid)
        {
            return ApiResponse<AuthResponseDto>.Fail("Credenciales inválidas.");
        }

        var roles = await _userManager.GetRolesAsync(user);

        // Obtener IDs de perfiles asociados
        var studentProfile = await _context.StudentProfiles.FirstOrDefaultAsync(s => s.UserId == user.Id);
        var teacherProfile = await _context.TeacherProfiles.FirstOrDefaultAsync(t => t.UserId == user.Id);
        var guardianProfile = await _context.GuardianProfiles.FirstOrDefaultAsync(g => g.UserId == user.Id);

        var (token, expiration) = _jwtService.GenerateAccessToken(
            user, 
            roles, 
            studentProfile?.Id, 
            teacherProfile?.Id, 
            guardianProfile?.Id);

        var refreshToken = _jwtService.GenerateRefreshToken();
        int refreshDurationDays = int.TryParse(_configuration["Jwt:RefreshTokenDurationInDays"], out var days) ? days : 7;

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(refreshDurationDays);
        await _userManager.UpdateAsync(user);

        var authResponse = new AuthResponseDto
        {
            Token = token,
            RefreshToken = refreshToken,
            Expiration = expiration,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                Nombres = user.Nombres,
                Apellidos = user.Apellidos,
                NombreCompleto = user.NombreCompleto,
                Roles = roles.ToList(),
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                EstudianteId = studentProfile?.Id,
                DocenteId = teacherProfile?.Id,
                AcudienteId = guardianProfile?.Id
            }
        };

        return ApiResponse<AuthResponseDto>.Ok(authResponse, "Inicio de sesión exitoso");
    }

    public async Task<ApiResponse<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto dto)
    {
        var principal = _jwtService.GetPrincipalFromExpiredToken(dto.AccessToken);
        if (principal == null)
        {
            return ApiResponse<AuthResponseDto>.Fail("Token de acceso inválido.");
        }

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier) ?? principal.FindFirst("sub");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return ApiResponse<AuthResponseDto>.Fail("Identificador de usuario no encontrado en el token.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || !user.IsActive)
        {
            return ApiResponse<AuthResponseDto>.Fail("Usuario no encontrado o inactivo.");
        }

        if (user.RefreshToken != dto.RefreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            return ApiResponse<AuthResponseDto>.Fail("El token de actualización ha expirado o no es válido.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var studentProfile = await _context.StudentProfiles.FirstOrDefaultAsync(s => s.UserId == user.Id);
        var teacherProfile = await _context.TeacherProfiles.FirstOrDefaultAsync(t => t.UserId == user.Id);
        var guardianProfile = await _context.GuardianProfiles.FirstOrDefaultAsync(g => g.UserId == user.Id);

        var (newToken, expiration) = _jwtService.GenerateAccessToken(
            user, 
            roles, 
            studentProfile?.Id, 
            teacherProfile?.Id, 
            guardianProfile?.Id);

        var newRefreshToken = _jwtService.GenerateRefreshToken();
        int refreshDurationDays = int.TryParse(_configuration["Jwt:RefreshTokenDurationInDays"], out var days) ? days : 7;

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(refreshDurationDays);
        await _userManager.UpdateAsync(user);

        var authResponse = new AuthResponseDto
        {
            Token = newToken,
            RefreshToken = newRefreshToken,
            Expiration = expiration,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                Nombres = user.Nombres,
                Apellidos = user.Apellidos,
                NombreCompleto = user.NombreCompleto,
                Roles = roles.ToList(),
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                EstudianteId = studentProfile?.Id,
                DocenteId = teacherProfile?.Id,
                AcudienteId = guardianProfile?.Id
            }
        };

        return ApiResponse<AuthResponseDto>.Ok(authResponse, "Token renovado exitosamente");
    }
}
