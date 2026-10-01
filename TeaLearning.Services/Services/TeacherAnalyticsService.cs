using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Data.Context;
using TeaLearning.Data.Repositories;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Analytics;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Services.Services;

public class TeacherAnalyticsService : ITeacherAnalyticsService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IActivityExecutionRepository _executionRepository;
    private readonly IActivityAssignmentRepository _assignmentRepository;
    private readonly ApplicationDbContext _context;

    public TeacherAnalyticsService(
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

    public async Task<ApiResponse<TeacherStudentSummaryDto>> GetStudentAnalyticsSummaryAsync(
        Guid studentId, 
        CancellationToken cancellationToken = default)
    {
        var student = await _studentRepository.GetStudentWithDetailsAsync(studentId, cancellationToken);
        if (student == null)
        {
            return ApiResponse<TeacherStudentSummaryDto>.Fail($"Estudiante con ID {studentId} no encontrado.");
        }

        var executions = await _executionRepository.GetByStudentIdWithActivityDetailsAsync(studentId, cancellationToken);
        var assignments = await _assignmentRepository.GetFilteredAsync(studentProfileId: studentId, cancellationToken: cancellationToken);

        int totalAciertos = executions.Sum(e => e.CantidadAciertos);
        int totalFallos = executions.Sum(e => e.CantidadIntentosFallidos);
        int totalRespuestas = totalAciertos + totalFallos;

        double tasaAciertosGlobal = totalRespuestas > 0 
            ? Math.Round((double)totalAciertos / totalRespuestas * 100.0, 1) 
            : 0.0;

        double promedioTiempo = executions.Any() 
            ? Math.Round(executions.Average(e => e.TiempoRespuestaSegundos), 1) 
            : 0.0;

        // 1. Tiempos promedio y aciertos por tipo de actividad
        var metricasPorTipo = executions
            .GroupBy(e => e.Assignment.Activity.TipoActividad)
            .Select(g =>
            {
                int aciertosTipo = g.Sum(x => x.CantidadAciertos);
                int fallosTipo = g.Sum(x => x.CantidadIntentosFallidos);
                int totalTipo = aciertosTipo + fallosTipo;

                return new ActivityTypeMetricDto
                {
                    TipoActividad = g.Key.ToString(),
                    TotalEjecuciones = g.Count(),
                    PromedioTiempoRespuestaSegundos = Math.Round(g.Average(x => x.TiempoRespuestaSegundos), 1),
                    TasaAciertosPorcentaje = totalTipo > 0 ? Math.Round((double)aciertosTipo / totalTipo * 100.0, 1) : 0.0
                };
            })
            .OrderBy(m => m.TipoActividad)
            .ToList();

        // 2. Curva de intentos (agrupado cronológicamente por día)
        var curvaIntentos = executions
            .GroupBy(e => e.FechaRegistro.Date)
            .Select(g => new AttemptsCurveDto
            {
                Fecha = g.Key,
                TotalIntentosFallidos = g.Sum(x => x.CantidadIntentosFallidos),
                TotalAciertos = g.Sum(x => x.CantidadAciertos),
                ActividadesEjecutadas = g.Count(),
                PromedioIntentosFallidosPorActividad = Math.Round(g.Average(x => x.CantidadIntentosFallidos), 2)
            })
            .OrderBy(c => c.Fecha)
            .ToList();

        // 3. Evolución semanal
        var evolucionSemanal = executions
            .GroupBy(e => CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(e.FechaRegistro, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday))
            .Select(g =>
            {
                var minDate = g.Min(x => x.FechaRegistro);
                int aciertosSem = g.Sum(x => x.CantidadAciertos);
                int fallosSem = g.Sum(x => x.CantidadIntentosFallidos);
                int totalSem = aciertosSem + fallosSem;

                return new WeeklyEvolutionDto
                {
                    SemanaEtiqueta = $"Semana {g.Key} ({minDate:MMM yyyy})",
                    FechaInicioSemana = minDate.Date,
                    ActividadesCompletadas = g.Count(x => x.CompletadaExitosamente),
                    TasaAciertosPorcentaje = totalSem > 0 ? Math.Round((double)aciertosSem / totalSem * 100.0, 1) : 0.0,
                    PromedioTiempoRespuestaSegundos = Math.Round(g.Average(x => x.TiempoRespuestaSegundos), 1)
                };
            })
            .OrderBy(w => w.FechaInicioSemana)
            .ToList();

        var dto = new TeacherStudentSummaryDto
        {
            StudentId = student.Id,
            NombreCompleto = student.User?.NombreCompleto ?? string.Empty,
            CodigoEstudiante = student.CodigoEstudiante,
            ClassroomNombre = student.Classroom?.Nombre ?? "Sin Aula Asignada",
            TasaAciertosGlobal = tasaAciertosGlobal,
            PromedioTiempoRespuestaSegundos = promedioTiempo,
            TotalActividadesCompletadas = assignments.Count(a => a.Estado == EstadoAsignacion.Completada),
            TotalActividadesPendientes = assignments.Count(a => a.Estado == EstadoAsignacion.Pendiente),
            TotalActividadesEnProgreso = assignments.Count(a => a.Estado == EstadoAsignacion.EnProgreso),
            TiemposPorTipoActividad = metricasPorTipo,
            CurvaIntentos = curvaIntentos,
            EvolucionSemanal = evolucionSemanal
        };

        return ApiResponse<TeacherStudentSummaryDto>.Ok(dto, "Resumen analítico del estudiante generado con éxito");
    }

    public async Task<ApiResponse<TeacherClassroomSummaryDto>> GetClassroomAnalyticsSummaryAsync(
        Guid? classroomId = null, 
        CancellationToken cancellationToken = default)
    {
        Classroom? classroom = null;
        if (classroomId.HasValue)
        {
            classroom = await _context.Classrooms
                .Include(c => c.Students.Where(s => s.IsActive))
                    .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(c => c.Id == classroomId.Value && c.IsActive, cancellationToken);
        }
        else
        {
            classroom = await _context.Classrooms
                .Include(c => c.Students.Where(s => s.IsActive))
                    .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(c => c.IsActive, cancellationToken);
        }

        if (classroom == null)
        {
            return ApiResponse<TeacherClassroomSummaryDto>.Fail($"Aula {(classroomId.HasValue ? $"con ID {classroomId.Value}" : "activa")} no encontrada.");
        }

        var executions = await _executionRepository.GetByClassroomIdWithDetailsAsync(classroom.Id, cancellationToken);

        // Incluir también estudiantes activos sin aula asignada
        var studentsWithoutClassroom = await _context.StudentProfiles
            .Include(s => s.User)
            .Where(s => s.IsActive && s.ClassroomId == null)
            .ToListAsync(cancellationToken);

        var allStudents = classroom.Students.Concat(studentsWithoutClassroom).ToList();

        int totalAciertos = executions.Sum(e => e.CantidadAciertos);
        int totalFallos = executions.Sum(e => e.CantidadIntentosFallidos);
        int totalRespuestas = totalAciertos + totalFallos;

        double tasaAciertosAula = totalRespuestas > 0 
            ? Math.Round((double)totalAciertos / totalRespuestas * 100.0, 1) 
            : 0.0;

        double promedioTiempoAula = executions.Any() 
            ? Math.Round(executions.Average(e => e.TiempoRespuestaSegundos), 1) 
            : 0.0;

        // Desempeño individual de cada estudiante (incluyendo los sin aula)
        var desempenoEstudiantes = new List<StudentClassPerformanceDto>();
        foreach (var student in allStudents)
        {
            var studentExecutions = executions.Where(e => e.StudentProfileId == student.Id).ToList();
            int stAciertos = studentExecutions.Sum(x => x.CantidadAciertos);
            int stFallos = studentExecutions.Sum(x => x.CantidadIntentosFallidos);
            int stTotal = stAciertos + stFallos;

            desempenoEstudiantes.Add(new StudentClassPerformanceDto
            {
                StudentId = student.Id,
                NombreCompleto = student.User?.NombreCompleto ?? string.Empty,
                CodigoEstudiante = student.CodigoEstudiante,
                ActividadesCompletadas = studentExecutions.Count(x => x.CompletadaExitosamente),
                TasaAciertosPorcentaje = stTotal > 0 ? Math.Round((double)stAciertos / stTotal * 100.0, 1) : 0.0,
                PromedioTiempoRespuestaSegundos = studentExecutions.Any() ? Math.Round(studentExecutions.Average(x => x.TiempoRespuestaSegundos), 1) : 0.0
            });
        }

        // Tiempos por tipo de actividad a nivel de aula
        var metricasPorTipo = executions
            .GroupBy(e => e.Assignment.Activity.TipoActividad)
            .Select(g =>
            {
                int aciertosTipo = g.Sum(x => x.CantidadAciertos);
                int fallosTipo = g.Sum(x => x.CantidadIntentosFallidos);
                int totalTipo = aciertosTipo + fallosTipo;

                return new ActivityTypeMetricDto
                {
                    TipoActividad = g.Key.ToString(),
                    TotalEjecuciones = g.Count(),
                    PromedioTiempoRespuestaSegundos = Math.Round(g.Average(x => x.TiempoRespuestaSegundos), 1),
                    TasaAciertosPorcentaje = totalTipo > 0 ? Math.Round((double)aciertosTipo / totalTipo * 100.0, 1) : 0.0
                };
            })
            .OrderBy(m => m.TipoActividad)
            .ToList();

        var dto = new TeacherClassroomSummaryDto
        {
            ClassroomId = classroom.Id,
            ClassroomNombre = classroom.Nombre,
            Grado = classroom.Grado,
            TotalEstudiantes = allStudents.Count,
            TasaAciertosPromedioAula = tasaAciertosAula,
            PromedioTiempoRespuestaAula = promedioTiempoAula,
            TotalActividadesCompletadasAula = executions.Count(e => e.CompletadaExitosamente),
            TiemposPorTipoActividad = metricasPorTipo,
            DesempenoEstudiantes = desempenoEstudiantes.OrderBy(d => d.NombreCompleto).ToList()
        };

        return ApiResponse<TeacherClassroomSummaryDto>.Ok(dto, "Métricas consolidadas del aula recuperadas con éxito");
    }
}
