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
        bool onlyActive = true,
        CancellationToken cancellationToken = default)
    {
        var activities = await _activityRepository.GetFilteredAsync(moduloId, tipoActividad, docenteId, onlyActive, cancellationToken);

        var dtoList = activities.Select(MapToDto).ToList();
        return ApiResponse<List<ActivityDto>>.Ok(dtoList, $"Se recuperaron {dtoList.Count} actividades pedagógicas");
    }

    public async Task<ApiResponse<ActivityDto>> GetActivityByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var activity = await _activityRepository.GetByIdWithDetailsAsync(id, includeInactive: false, cancellationToken);
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
        var module = await _moduleRepository.GetWithActivitiesAsync(dto.ModuloId, includeInactive: true, cancellationToken);
        if (module == null)
        {
            return ApiResponse<ActivityDto>.Fail($"El módulo pedagógico con ID {dto.ModuloId} no existe.");
        }

        // 2. Obtener o crear el perfil de docente propio del usuario autenticado (nunca asignar a otro docente ajeno)
        var teacherProfile = await _context.TeacherProfiles
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.UserId == teacherUserId, cancellationToken);

        if (teacherProfile == null)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == teacherUserId && u.IsActive, cancellationToken);
            if (!userExists)
            {
                return ApiResponse<ActivityDto>.Fail("No se pudo verificar el usuario autenticado para crear la actividad.");
            }

            teacherProfile = new TeacherProfile
            {
                UserId = teacherUserId,
                Especialidad = "Educación Especial y TEA Nivel 1",
                NumeroRegistroProfesional = $"REG-DOC-{DateTime.UtcNow.Ticks % 1000000:D6}",
                IsActive = true
            };
            _context.TeacherProfiles.Add(teacherProfile);
            await _context.SaveChangesAsync(cancellationToken);
        }

        // 3. Validar sintaxis y contenido mínimo del JSON dinámico (ConfigJson)
        var configValidationError = ValidateActivityConfig(dto.TipoActividad, dto.ConfigJson);
        if (configValidationError != null)
        {
            return ApiResponse<ActivityDto>.Fail(configValidationError);
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
            IsActive = dto.IsActive
        };

        await _activityRepository.AddAsync(activity, cancellationToken);

        // Recargar con relaciones (incluyendo si se creó inicialmente como inactiva)
        var createdWithDetails = await _activityRepository.GetByIdWithDetailsAsync(activity.Id, includeInactive: true, cancellationToken);
        return ApiResponse<ActivityDto>.Ok(MapToDto(createdWithDetails ?? activity), "Actividad pedagógica creada exitosamente");
    }

    public async Task<ApiResponse<ActivityDto>> UpdateActivityAsync(
        Guid id, 
        Guid teacherUserId, 
        UpdateActivityDto dto, 
        bool isAdmin = false, 
        CancellationToken cancellationToken = default)
    {
        var activity = await _activityRepository.GetByIdWithDetailsAsync(id, includeInactive: true, cancellationToken);
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
        var module = await _moduleRepository.GetWithActivitiesAsync(dto.ModuloId, includeInactive: true, cancellationToken);
        if (module == null)
        {
            return ApiResponse<ActivityDto>.Fail($"El módulo pedagógico con ID {dto.ModuloId} no existe.");
        }

        // Validar ConfigJson
        var configValidationError = ValidateActivityConfig(dto.TipoActividad, dto.ConfigJson);
        if (configValidationError != null)
        {
            return ApiResponse<ActivityDto>.Fail(configValidationError);
        }

        activity.Titulo = dto.Titulo.Trim();
        activity.Descripcion = dto.Descripcion.Trim();
        activity.TipoActividad = dto.TipoActividad;
        activity.ObjetivoPedagogico = dto.ObjetivoPedagogico.Trim();
        activity.ConfigJson = dto.ConfigJson;
        activity.ModuloId = dto.ModuloId;
        if (dto.IsActive.HasValue)
        {
            activity.IsActive = dto.IsActive.Value;
        }

        await _activityRepository.UpdateAsync(activity, cancellationToken);

        var updatedWithDetails = await _activityRepository.GetByIdWithDetailsAsync(id, includeInactive: true, cancellationToken);
        return ApiResponse<ActivityDto>.Ok(MapToDto(updatedWithDetails ?? activity), "Actividad pedagógica actualizada exitosamente");
    }

    public async Task<ApiResponse<bool>> DeleteActivityAsync(
        Guid id, 
        Guid teacherUserId, 
        bool isAdmin = false, 
        CancellationToken cancellationToken = default)
    {
        var activity = await _activityRepository.GetByIdWithDetailsAsync(id, includeInactive: true, cancellationToken);
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

        return ApiResponse<bool>.Ok(true, "Actividad pedagógica eliminada exitosamente");
    }

    private static string? ValidateActivityConfig(TipoActividad tipoActividad, string strInput)
    {
        if (string.IsNullOrWhiteSpace(strInput) || strInput.Length > 16384)
        {
            return "La estructura de ConfigJson es obligatoria y no debe superar los 16 KB.";
        }

        string[] dangerousPatterns = ["<script", "javascript:", "onerror=", "onload=", "<iframe", "vbscript:", "data:text/html"];
        foreach (var pattern in dangerousPatterns)
        {
            if (strInput.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return "La estructura de ConfigJson contiene contenido no permitido.";
            }
        }

        try
        {
            using var doc = JsonDocument.Parse(strInput, new JsonDocumentOptions
            {
                MaxDepth = 8
            });

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return "La estructura de ConfigJson debe ser un objeto JSON válido.";
            }

            var root = doc.RootElement;
            if (!root.EnumerateObject().Any())
            {
                return "La actividad no puede guardarse con una configuración vacía.";
            }

            if (tipoActividad == TipoActividad.SecuenciaLogica)
            {
                JsonElement stepsEl = default;
                bool hasSteps = (root.TryGetProperty("pasos", out stepsEl) && stepsEl.ValueKind == JsonValueKind.Array)
                             || (root.TryGetProperty("steps", out stepsEl) && stepsEl.ValueKind == JsonValueKind.Array);

                if (!hasSteps)
                {
                    return "La secuencia lógica debe incluir la lista de pasos.";
                }

                int validCount = 0;
                foreach (var item in stepsEl.EnumerateArray())
                {
                    string? label = item.ValueKind == JsonValueKind.String
                        ? item.GetString()
                        : (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("label", out var lbl) && lbl.ValueKind == JsonValueKind.String
                            ? lbl.GetString()
                            : null);

                    if (!string.IsNullOrWhiteSpace(label))
                    {
                        validCount++;
                    }
                }

                if (validCount < 2)
                {
                    return "La actividad de secuencia lógica requiere al menos 2 pasos con texto.";
                }
            }
            else if (tipoActividad == TipoActividad.EmparejamientoVisual)
            {
                JsonElement pairsEl = default;
                bool hasPairs = (root.TryGetProperty("parejas", out pairsEl) && pairsEl.ValueKind == JsonValueKind.Array)
                             || (root.TryGetProperty("pairs", out pairsEl) && pairsEl.ValueKind == JsonValueKind.Array)
                             || (root.TryGetProperty("pares", out pairsEl) && pairsEl.ValueKind == JsonValueKind.Array);

                if (!hasPairs)
                {
                    return "La actividad de emparejamiento visual debe incluir la lista de parejas.";
                }

                int validCount = 0;
                foreach (var item in pairsEl.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;

                    string? left = GetFirstString(item, "leftLabel", "emocion", "elementoA");
                    string? right = GetFirstString(item, "rightLabel", "pareja", "elementoB", "color");

                    bool leftEmpty = string.IsNullOrWhiteSpace(left);
                    bool rightEmpty = string.IsNullOrWhiteSpace(right);

                    if (leftEmpty != rightEmpty)
                    {
                        return "Todas las parejas de emparejamiento deben tener completos ambos elementos (izquierdo y derecho).";
                    }

                    if (!leftEmpty && !rightEmpty)
                    {
                        validCount++;
                    }
                }

                if (validCount < 2)
                {
                    return "La actividad de emparejamiento visual requiere al menos 2 parejas completas.";
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return "La estructura de ConfigJson no es un JSON válido.";
        }
    }

    private static string? GetFirstString(JsonElement obj, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (obj.TryGetProperty(k, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }
        return null;
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
            IsActive = a.IsActive,
            CreatedAt = a.CreatedAt
        };
    }
}
