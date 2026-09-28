using TeaLearning.Core.Enums;
using TeaLearning.Data.Repositories;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Guardian;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Services.Services;

public class GuardianMonitoringService : IGuardianMonitoringService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IActivityAssignmentRepository _assignmentRepository;
    private readonly IActivityExecutionRepository _executionRepository;

    public GuardianMonitoringService(
        IStudentRepository studentRepository,
        IActivityAssignmentRepository assignmentRepository,
        IActivityExecutionRepository executionRepository)
    {
        _studentRepository = studentRepository;
        _assignmentRepository = assignmentRepository;
        _executionRepository = executionRepository;
    }

    public async Task<ApiResponse<GuardianStudentProgressDto>> GetStudentProgressAsync(
        Guid studentId, 
        Guid currentUserId, 
        bool isAdmin, 
        CancellationToken cancellationToken = default)
    {
        var student = await _studentRepository.GetStudentWithDetailsAsync(studentId, cancellationToken);
        if (student == null)
        {
            return ApiResponse<GuardianStudentProgressDto>.Fail($"Estudiante con ID {studentId} no encontrado.");
        }

        // Validación de autorización: Sólo el acudiente titular del estudiante o un administrador pueden acceder
        if (!isAdmin)
        {
            if (student.GuardianProfile == null || student.GuardianProfile.UserId != currentUserId)
            {
                return ApiResponse<GuardianStudentProgressDto>.Fail("No tiene autorización para consultar la información pedagógica de este estudiante.");
            }
        }

        var assignments = await _assignmentRepository.GetFilteredAsync(studentProfileId: studentId, cancellationToken: cancellationToken);
        var executions = await _executionRepository.GetByStudentIdWithActivityDetailsAsync(studentId, cancellationToken);

        int totalAsignadas = assignments.Count;
        int completadas = assignments.Count(a => a.Estado == EstadoAsignacion.Completada);
        int enProgreso = assignments.Count(a => a.Estado == EstadoAsignacion.EnProgreso);
        int pendientes = assignments.Count(a => a.Estado == EstadoAsignacion.Pendiente);

        double porcentajeAvance = totalAsignadas > 0 
            ? Math.Round((double)completadas / totalAsignadas * 100.0, 1) 
            : 0.0;

        int totalSegundosInvertidos = executions.Sum(e => e.TiempoRespuestaSegundos);
        double minutosInvertidos = Math.Round(totalSegundosInvertidos / 60.0, 1);

        var ultimasActividades = executions
            .OrderByDescending(e => e.FechaRegistro)
            .Take(5)
            .Select(e => new RecentActivityDto
            {
                ActivityTitulo = e.Assignment?.Activity?.Titulo ?? "Actividad pedagógica",
                TipoActividad = e.Assignment?.Activity?.TipoActividad.ToString() ?? "Actividad",
                FechaRegistro = e.FechaRegistro,
                CompletadaExitosamente = e.CompletadaExitosamente,
                CantidadAciertos = e.CantidadAciertos,
                CantidadIntentosFallidos = e.CantidadIntentosFallidos,
                TiempoRespuestaSegundos = e.TiempoRespuestaSegundos
            })
            .ToList();

        var historico = executions
            .GroupBy(e =>
            {
                var d = e.FechaRegistro.Date;
                int diff = (7 + (d.DayOfWeek - DayOfWeek.Monday)) % 7;
                return d.AddDays(-1 * diff).ToString("yyyy-MM-dd");
            })
            .OrderBy(g => g.Key)
            .Select(g => new GuardianWeeklyProgressDto
            {
                Periodo = g.Key,
                ActividadesCompletadas = g.Count(e => e.CompletadaExitosamente),
                TasaAciertos = g.Sum(e => e.CantidadAciertos + e.CantidadIntentosFallidos) > 0
                    ? Math.Round(100.0 * g.Sum(e => e.CantidadAciertos) / g.Sum(e => e.CantidadAciertos + e.CantidadIntentosFallidos), 1)
                    : 100.0
            })
            .ToList();

        string nombreEstudiante = student.User?.Nombres ?? "El estudiante";
        string mensajePedagogico = GenerarMensajePedagogico(nombreEstudiante, porcentajeAvance, completadas);

        var dto = new GuardianStudentProgressDto
        {
            StudentId = student.Id,
            StudentNombre = student.User?.NombreCompleto ?? string.Empty,
            CodigoEstudiante = student.CodigoEstudiante,
            ActividadesAsignadasTotal = totalAsignadas,
            ActividadesCompletadas = completadas,
            ActividadesEnProgreso = enProgreso,
            ActividadesPendientes = pendientes,
            PorcentajeAvance = porcentajeAvance,
            TiempoTotalInvertidoMinutos = minutosInvertidos,
            UltimasActividades = ultimasActividades,
            Historico = historico,
            MensajePedagogico = mensajePedagogico
        };

        return ApiResponse<GuardianStudentProgressDto>.Ok(dto, "Progreso pedagógico del estudiante recuperado exitosamente.");
    }

    public async Task<ApiResponse<ActivityHistoryPagedDto>> GetActivityHistoryAsync(
        Guid studentId,
        Guid currentUserId,
        bool isAdmin,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var student = await _studentRepository.GetStudentWithDetailsAsync(studentId, cancellationToken);
        if (student == null)
        {
            return ApiResponse<ActivityHistoryPagedDto>.Fail($"Estudiante con ID {studentId} no encontrado.");
        }

        if (!isAdmin)
        {
            if (student.GuardianProfile == null || student.GuardianProfile.UserId != currentUserId)
            {
                return ApiResponse<ActivityHistoryPagedDto>.Fail("No tiene autorización para consultar el historial de este estudiante.");
            }
        }

        var executions = await _executionRepository.GetByStudentIdWithActivityDetailsAsync(studentId, cancellationToken);
        var ordered = executions.OrderByDescending(e => e.FechaRegistro).ToList();
        int total = ordered.Count;
        int safePage = Math.Max(1, page);
        int safePageSize = Math.Max(1, pageSize);

        var items = ordered
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(e => new RecentActivityHistoryDto
            {
                Titulo = e.Assignment?.Activity?.Titulo ?? "Actividad pedagógica",
                TipoActividad = e.Assignment?.Activity?.TipoActividad.ToString() ?? "Actividad",
                FechaRegistro = e.FechaRegistro,
                CompletadaExitosamente = e.CompletadaExitosamente,
                CantidadAciertos = e.CantidadAciertos,
                CantidadIntentosFallidos = e.CantidadIntentosFallidos,
                TiempoRespuestaSegundos = e.TiempoRespuestaSegundos
            })
            .ToList();

        var result = new ActivityHistoryPagedDto
        {
            Items = items,
            Total = total,
            Page = safePage,
            PageSize = safePageSize
        };

        return ApiResponse<ActivityHistoryPagedDto>.Ok(result, "Historial de actividades recuperado exitosamente.");
    }

    private static string GenerarMensajePedagogico(string nombre, double porcentajeAvance, int completadas)
    {
        if (porcentajeAvance >= 80)
        {
            return $"¡Excelente avance! {nombre} ha demostrado gran dedicación y constancia completando la gran mayoría de sus actividades planificadas.";
        }
        if (porcentajeAvance >= 50)
        {
            return $"¡Muy buen ritmo! {nombre} avanza con pasos sólidos y perseverancia en sus actividades pedagógicas.";
        }
        if (completadas > 0)
        {
            return $"¡Buen comienzo! {nombre} está explorando y afianzando sus actividades a su propio ritmo.";
        }
        return $"{nombre} cuenta con actividades preparadas para comenzar cuando se sienta con la disposición y calma adecuadas.";
    }
}
