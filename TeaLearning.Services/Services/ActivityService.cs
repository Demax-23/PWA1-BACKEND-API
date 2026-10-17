using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Data.Context;
using TeaLearning.Data.Repositories;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Activities;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Services.Services;

public class ActivityService : IActivityService
{
    private readonly IActivityRepository _activityRepository;
    private readonly ILearningModuleRepository _moduleRepository;
    private readonly ApplicationDbContext _context;

    public ActivityService(
        IActivityRepository activityRepository,
        ILearningModuleRepository moduleRepository,
        ApplicationDbContext context)
    {
        _activityRepository = activityRepository;
        _moduleRepository = moduleRepository;
        _context = context;
    }

    public async Task<ApiResponse<List<ActivityDto>>> GetActivitiesAsync(
        Guid? moduloId = null, 
        TipoActividad? tipoActividad = null, 
        Guid? docenteId = null, 
        CancellationToken cancellationToken = default)
    {
        var activities = await _activityRepository.GetFilteredAsync(moduloId, tipoActividad, docenteId, cancellationToken);

        var dtoList = activities.Select(MapToDto).ToList();
        return ApiResponse<List<ActivityDto>>.Ok(dtoList, $"Se recuperaron {dtoList.Count} actividades pedagógicas");
    }

    public async Task<ApiResponse<ActivityDto>> GetActivityByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var activity = await _activityRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (activity == null)
        {
            return ApiResponse<ActivityDto>.Fail($"Actividad pedagógica con ID {id} no encontrada.");
        }

        return ApiResponse<ActivityDto>.Ok(MapToDto(activity), "Actividad pedagógica recuperada con éxito");
    }

    public async Task<ApiResponse<ActivityDto>> CreateActivityAsync(
        Guid teacherUserId, 
        CreateActivityDto dto, 
        CancellationToken cancellationToken = default)
    {
        // 1. Validar que el módulo exista
        var module = await _moduleRepository.GetByIdAsync(dto.ModuloId, cancellationToken);
        if (module == null)
        {
            return ApiResponse<ActivityDto>.Fail($"El módulo pedagógico con ID {dto.ModuloId} no existe.");
        }

        // 2. Obtener o validar el perfil de docente del usuario autenticado
        var teacherProfile = await _context.TeacherProfiles
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.UserId == teacherUserId, cancellationToken);

        if (teacherProfile == null)
        {
            teacherProfile = await _context.TeacherProfiles.FirstOrDefaultAsync(cancellationToken);
            if (teacherProfile == null)
            {
                teacherProfile = new TeacherProfile
                {
                    UserId = teacherUserId,
                    Especialidad = "Educación Especial y TEA Nivel 1",
                    NumeroRegistroProfesional = "REG-DOC-AUTO-01"
                };
                _context.TeacherProfiles.Add(teacherProfile);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        // 3. Validar sintaxis del JSON dinámico (ConfigJson)
        if (!IsValidJson(dto.ConfigJson))
        {
            return ApiResponse<ActivityDto>.Fail("La estructura de ConfigJson no es un JSON válido.");
        }

        var activity = new Activity
        {
            Titulo = dto.Titulo.Trim(),
            Descripcion = dto.Descripcion.Trim(),
            TipoActividad = dto.TipoActividad,
            ObjetivoPedagogico = dto.ObjetivoPedagogico.Trim(),
            ConfigJson = dto.ConfigJson,
            ModuloId = dto.ModuloId,
            DocenteId = teacherProfile.Id,
            IsActive = true
        };

        await _activityRepository.AddAsync(activity, cancellationToken);

        // Recargar con relaciones
        var createdWithDetails = await _activityRepository.GetByIdWithDetailsAsync(activity.Id, cancellationToken);
        return ApiResponse<ActivityDto>.Ok(MapToDto(createdWithDetails ?? activity), "Actividad pedagógica creada exitosamente");
    }

    public async Task<ApiResponse<ActivityDto>> UpdateActivityAsync(
        Guid id, 
        Guid teacherUserId, 
        UpdateActivityDto dto, 
        bool isAdmin = false, 
        CancellationToken cancellationToken = default)
    {
        var activity = await _activityRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (activity == null)
        {
            return ApiResponse<ActivityDto>.Fail($"Actividad con ID {id} no encontrada.");
        }

        // Si no es admin, verificar que sea el docente autor
        if (!isAdmin)
        {
            var teacherProfile = await _context.TeacherProfiles
                .FirstOrDefaultAsync(t => t.UserId == teacherUserId, cancellationToken);

            if (teacherProfile == null || activity.DocenteId != teacherProfile.Id)
            {
                return ApiResponse<ActivityDto>.Fail("No tiene permisos para modificar una actividad creada por otro docente.");
            }
        }

        // Validar módulo
        var module = await _moduleRepository.GetByIdAsync(dto.ModuloId, cancellationToken);
        if (module == null)
        {
            return ApiResponse<ActivityDto>.Fail($"El módulo pedagógico con ID {dto.ModuloId} no existe.");
        }

        // Validar ConfigJson
        if (!IsValidJson(dto.ConfigJson))
        {
            return ApiResponse<ActivityDto>.Fail("La estructura de ConfigJson no es un JSON válido.");
        }

        activity.Titulo = dto.Titulo.Trim();
        activity.Descripcion = dto.Descripcion.Trim();
        activity.TipoActividad = dto.TipoActividad;
        activity.ObjetivoPedagogico = dto.ObjetivoPedagogico.Trim();
        activity.ConfigJson = dto.ConfigJson;
        activity.ModuloId = dto.ModuloId;

        await _activityRepository.UpdateAsync(activity, cancellationToken);

        var updatedWithDetails = await _activityRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        return ApiResponse<ActivityDto>.Ok(MapToDto(updatedWithDetails ?? activity), "Actividad pedagógica actualizada exitosamente");
    }

    public async Task<ApiResponse<bool>> DeleteActivityAsync(
        Guid id, 
        Guid teacherUserId, 
        bool isAdmin = false, 
        CancellationToken cancellationToken = default)
    {
        var activity = await _activityRepository.GetByIdAsync(id, cancellationToken);
        if (activity == null)
        {
            return ApiResponse<bool>.Fail($"Actividad con ID {id} no encontrada.");
        }

        if (!isAdmin)
        {
            var teacherProfile = await _context.TeacherProfiles
                .FirstOrDefaultAsync(t => t.UserId == teacherUserId, cancellationToken);

            if (teacherProfile == null || activity.DocenteId != teacherProfile.Id)
            {
                return ApiResponse<bool>.Fail("No tiene permisos para eliminar una actividad creada por otro docente.");
            }
        }

        await _activityRepository.DeleteAsync(activity, cancellationToken);

        var relatedAssignments = await _context.ActivityAssignments
            .Where(aa => aa.ActivityId == id && aa.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var asg in relatedAssignments)
        {
            asg.IsActive = false;
        }
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, "Actividad pedagógica eliminada exitosamente");
    }

    private static bool IsValidJson(string strInput)
    {
        if (string.IsNullOrWhiteSpace(strInput) || strInput.Length > 16384)
        {
            return false;
        }

        string[] dangerousPatterns = ["<script", "javascript:", "onerror=", "onload=", "<iframe", "vbscript:", "data:text/html"];
        foreach (var pattern in dangerousPatterns)
        {
            if (strInput.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        try
        {
            using var doc = JsonDocument.Parse(strInput, new JsonDocumentOptions
            {
                MaxDepth = 8
            });
            return doc.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static ActivityDto MapToDto(Activity a)
    {
        return new ActivityDto
        {
            Id = a.Id,
            Titulo = a.Titulo,
            Descripcion = a.Descripcion,
            TipoActividad = a.TipoActividad,
            ObjetivoPedagogico = a.ObjetivoPedagogico,
            ConfigJson = a.ConfigJson,
            ModuloId = a.ModuloId,
            ModuloTitulo = a.Modulo?.Titulo ?? string.Empty,
            DocenteId = a.DocenteId,
            DocenteNombre = a.Docente?.User?.NombreCompleto ?? string.Empty,
            CreatedAt = a.CreatedAt
        };
    }
}
