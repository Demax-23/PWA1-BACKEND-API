using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Data.Context;
using TeaLearning.Data.Repositories;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Achievements;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Services.Services;

public class AchievementService : IAchievementService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IActivityExecutionRepository _executionRepository;
    private readonly IActivityAssignmentRepository _assignmentRepository;
    private readonly ApplicationDbContext _context;

    public AchievementService(
        IStudentRepository studentRepository,
        IActivityExecutionRepository executionRepository,
        IActivityAssignmentRepository assignmentRepository,
        ApplicationDbContext context)
    {
        _studentRepository = studentRepository;
        _executionRepository = executionRepository;
        _assignmentRepository = assignmentRepository;
        _context = context;
    }

    public async Task<ApiResponse<StudentAchievementsDto>> GetStudentAchievementsByUserIdAsync(
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        var student = await _studentRepository.GetByUserIdAsync(userId, cancellationToken);
        if (student == null)
        {
            return ApiResponse<StudentAchievementsDto>.Fail("No se encontró el perfil de estudiante para este usuario.");
        }

        return await BuildAchievementsDtoAsync(student, cancellationToken);
    }

    public async Task<ApiResponse<StudentAchievementsDto>> GetStudentAchievementsByIdAsync(
        Guid studentId, 
        CancellationToken cancellationToken = default)
    {
        var student = await _studentRepository.GetStudentWithDetailsAsync(studentId, cancellationToken);
        if (student == null)
        {
            return ApiResponse<StudentAchievementsDto>.Fail($"Estudiante con ID {studentId} no encontrado.");
        }

        return await BuildAchievementsDtoAsync(student, cancellationToken);
    }

    private async Task<ApiResponse<StudentAchievementsDto>> BuildAchievementsDtoAsync(
        StudentProfile student, 
        CancellationToken cancellationToken)
    {
        var executions = await _executionRepository.GetByStudentIdWithActivityDetailsAsync(student.Id, cancellationToken);
        var assignments = await _assignmentRepository.GetFilteredAsync(studentProfileId: student.Id, cancellationToken: cancellationToken);

        var modules = await _context.LearningModules
            .Include(m => m.Activities.Where(a => a.IsActive))
            .Where(m => m.IsActive)
            .OrderBy(m => m.Orden)
            .ToListAsync(cancellationToken);

        var exitos = executions.Where(e => e.CompletadaExitosamente).OrderBy(e => e.FechaRegistro).ToList();

        // 1. Cálculo de Insignias Personales (Cero competitividad, basadas en el esfuerzo individual)
        var primerExito = exitos.FirstOrDefault();
        var visualExito = exitos.FirstOrDefault(e => e.Assignment?.Activity?.TipoActividad == TipoActividad.EmparejamientoVisual);
        var secuenciaExito = exitos.FirstOrDefault(e => e.Assignment?.Activity?.TipoActividad == TipoActividad.SecuenciaLogica);
        var emocionalExito = exitos.FirstOrDefault(e => e.Assignment?.Activity?.TipoActividad == TipoActividad.AsociacionEmocional);
        var socialExito = exitos.FirstOrDefault(e => e.Assignment?.Activity?.TipoActividad == TipoActividad.RutinaSocial);

        var insignias = new List<AchievementBadgeDto>
        {
            new AchievementBadgeDto
            {
                Clave = "primer_paso",
                Titulo = "Primer Paso",
                Descripcion = "Completaste tu primera actividad de aprendizaje en la plataforma.",
                Icono = "award",
                Desbloqueado = primerExito != null,
                FechaDesbloqueo = primerExito?.FechaRegistro
            },
            new AchievementBadgeDto
            {
                Clave = "explorador_visual",
                Titulo = "Explorador Visual",
                Descripcion = "Completaste con éxito una actividad de emparejamiento visual.",
                Icono = "eye",
                Desbloqueado = visualExito != null,
                FechaDesbloqueo = visualExito?.FechaRegistro
            },
            new AchievementBadgeDto
            {
                Clave = "maestro_secuencias",
                Titulo = "Maestro de Secuencias",
                Descripcion = "Organizaste los pasos en una actividad de secuencia lógica.",
                Icono = "list-ordered",
                Desbloqueado = secuenciaExito != null,
                FechaDesbloqueo = secuenciaExito?.FechaRegistro
            },
            new AchievementBadgeDto
            {
                Clave = "campeon_calma",
                Titulo = "Campeón de la Calma",
                Descripcion = "Exploraste y reconociste emociones en actividades de asociación emocional.",
                Icono = "heart",
                Desbloqueado = emocionalExito != null,
                FechaDesbloqueo = emocionalExito?.FechaRegistro
            },
            new AchievementBadgeDto
            {
                Clave = "constructor_vinculos",
                Titulo = "Constructor de Vínculos",
                Descripcion = "Completaste una actividad de interacción y rutina social.",
                Icono = "users",
                Desbloqueado = socialExito != null,
                FechaDesbloqueo = socialExito?.FechaRegistro
            },
            new AchievementBadgeDto
            {
                Clave = "habito_constante",
                Titulo = "Hábito Constante",
                Descripcion = "Superaste 5 actividades demostrando gran constancia y dedicación.",
                Icono = "check-circle",
                Desbloqueado = exitos.Count >= 5,
                FechaDesbloqueo = exitos.Count >= 5 ? exitos[4].FechaRegistro : null
            },
            new AchievementBadgeDto
            {
                Clave = "super_perseverante",
                Titulo = "Super Perseverante",
                Descripcion = "Alcanzaste 10 actividades completadas a tu propio ritmo.",
                Icono = "sparkles",
                Desbloqueado = exitos.Count >= 10,
                FechaDesbloqueo = exitos.Count >= 10 ? exitos[9].FechaRegistro : null
            }
        };

        // 2. Módulos completados (un módulo está completado si todas sus actividades activas tienen al menos una asignación completada)
        var modulosConcluidos = new List<CompletedModuleDto>();
        var completedActivityIds = assignments
            .Where(a => a.Estado == EstadoAsignacion.Completada)
            .Select(a => a.ActivityId)
            .ToHashSet();

        foreach (var mod in modules)
        {
            if (mod.Activities.Any() && mod.Activities.All(a => completedActivityIds.Contains(a.Id)))
            {
                var lastExecutionDate = executions
                    .Where(e => e.CompletadaExitosamente && mod.Activities.Any(act => act.Id == e.Assignment?.ActivityId))
                    .OrderByDescending(e => e.FechaRegistro)
                    .Select(e => (DateTime?)e.FechaRegistro)
                    .FirstOrDefault();

                modulosConcluidos.Add(new CompletedModuleDto
                {
                    ModuloId = mod.Id,
                    Titulo = mod.Titulo,
                    TotalActividades = mod.Activities.Count,
                    FechaConclusion = lastExecutionDate ?? DateTime.UtcNow
                });
            }
        }

        string nombreEstudiante = student.User?.Nombres ?? "Estudiante";
        int totalCompletadas = exitos.Count;
        string mensajeMotivacional = totalCompletadas switch
        {
            0 => $"¡Hola {nombreEstudiante}! Tienes grandes aventuras de aprendizaje esperando por ti.",
            < 5 => $"¡Buen trabajo {nombreEstudiante}! Cada paso que das te ayuda a descubrir cosas nuevas.",
            < 10 => $"¡Increíble dedicación {nombreEstudiante}! Estás construyendo grandes habilidades a tu propio ritmo.",
            _ => $"¡Orgullo total {nombreEstudiante}! Tu esfuerzo y constancia son admirables."
        };

        var dto = new StudentAchievementsDto
        {
            EstudianteId = student.Id,
            NombreCompleto = student.User?.NombreCompleto ?? string.Empty,
            TotalActividadesCompletadas = totalCompletadas,
            TotalModulosConcluidos = modulosConcluidos.Count,
            Insignias = insignias,
            ModulosConcluidos = modulosConcluidos,
            MensajeMotivacional = mensajeMotivacional
        };

        return ApiResponse<StudentAchievementsDto>.Ok(dto, "Logros e insignias del estudiante recuperados exitosamente.");
    }
}
