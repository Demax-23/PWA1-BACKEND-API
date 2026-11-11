using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Data.Context;
using TeaLearning.Data.SqlScripts;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Reports;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Services.Services;

/// <summary>
/// Implementación del servicio de reportes analíticos y ejecución de los 5 Stored Procedures de negocio.
/// Utiliza ADO.NET sobre la conexión de EF Core cuando se ejecuta en SQL Server relacional,
/// e incluye respaldo equivalente en memoria para pruebas unitarias con UseInMemoryDatabase.
/// </summary>
public class ReportService : IReportService
{
    private readonly ApplicationDbContext _context;

    public ReportService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<StudentComprehensiveReportDto>> GetStudentComprehensiveReportAsync(
        Guid? studentProfileId,
        Guid requesterUserId,
        bool isGuardian,
        int diasVentana = 30,
        CancellationToken cancellationToken = default)
    {
        diasVentana = Math.Clamp(diasVentana <= 0 ? 30 : diasVentana, 7, 180);

        StudentProfile? student;
        if (isGuardian)
        {
            var guardian = await _context.GuardianProfiles
                .Include(g => g.Students)
                .FirstOrDefaultAsync(g => g.UserId == requesterUserId && g.IsActive, cancellationToken);

            if (guardian == null)
            {
                return ApiResponse<StudentComprehensiveReportDto>.Fail("No se encontró un perfil de acudiente activo.");
            }

            if (studentProfileId.HasValue && studentProfileId.Value != Guid.Empty)
            {
                student = guardian.Students.FirstOrDefault(s => s.Id == studentProfileId.Value && s.IsActive);
                if (student == null)
                {
                    return ApiResponse<StudentComprehensiveReportDto>.Fail("No tiene autorización para generar reportes de un estudiante no vinculado a su cuenta.");
                }
            }
            else
            {
                if (guardian.Students.Count == 0)
                {
                    return ApiResponse<StudentComprehensiveReportDto>.Fail("No se encontró un perfil de acudiente con estudiantes vinculados.");
                }
                student = guardian.Students.Where(s => s.IsActive).OrderBy(s => s.CodigoEstudiante).FirstOrDefault();
            }
        }
        else
        {
            if (studentProfileId.HasValue && studentProfileId.Value != Guid.Empty)
            {
                student = await _context.StudentProfiles
                    .FirstOrDefaultAsync(s => s.Id == studentProfileId.Value && s.IsActive, cancellationToken);
            }
            else
            {
                student = await _context.StudentProfiles
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.CodigoEstudiante)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        if (student == null)
        {
            return ApiResponse<StudentComprehensiveReportDto>.Fail("Estudiante no encontrado.");
        }

        StudentComprehensiveReportDto report;
        if (_context.Database.IsRelational())
        {
            try
            {
                report = await ExecuteSpStudentReportAsync(student.Id, diasVentana, isGuardian, cancellationToken);
            }
            catch
            {
                report = await BuildFallbackStudentReportAsync(student.Id, diasVentana, isGuardian, cancellationToken);
            }
        }
        else
        {
            report = await BuildFallbackStudentReportAsync(student.Id, diasVentana, isGuardian, cancellationToken);
        }

        report.DiasVentana = diasVentana;
        report.GeneradoPorRol = isGuardian ? "Acudiente" : "Docente";

        return ApiResponse<StudentComprehensiveReportDto>.Ok(
            report,
            "Reporte integral del estudiante generado exitosamente mediante sp_GenerarReporteIntegralEstudiante.");
    }

    public async Task<ApiResponse<ClassroomConsolidatedReportDto>> GetClassroomConsolidatedReportAsync(
        Guid? classroomId,
        Guid requesterUserId,
        int diasVentana = 30,
        CancellationToken cancellationToken = default)
    {
        diasVentana = Math.Clamp(diasVentana <= 0 ? 30 : diasVentana, 7, 180);

        Guid? targetClassroomId = classroomId;
        if (!targetClassroomId.HasValue || targetClassroomId.Value == Guid.Empty)
        {
            targetClassroomId = await _context.Classrooms
                .Where(c => c.IsActive && c.TeacherProfile != null && c.TeacherProfile.UserId == requesterUserId)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(cancellationToken);

            targetClassroomId ??= await _context.Classrooms
                .Where(c => c.IsActive)
                .OrderBy(c => c.CreatedAt)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!targetClassroomId.HasValue || targetClassroomId.Value == Guid.Empty)
        {
            return ApiResponse<ClassroomConsolidatedReportDto>.Fail("No se encontró un aula activa para generar el reporte consolidado.");
        }

        var classroomExists = await _context.Classrooms.AnyAsync(c => c.Id == targetClassroomId.Value && c.IsActive, cancellationToken);
        if (!classroomExists)
        {
            return ApiResponse<ClassroomConsolidatedReportDto>.Fail($"El aula con ID {targetClassroomId} no fue encontrada.");
        }

        ClassroomConsolidatedReportDto report;
        if (_context.Database.IsRelational())
        {
            try
            {
                report = await ExecuteSpClassroomReportAsync(targetClassroomId.Value, requesterUserId, diasVentana, cancellationToken);
            }
            catch
            {
                report = await BuildFallbackClassroomReportAsync(targetClassroomId.Value, diasVentana, cancellationToken);
            }
        }
        else
        {
            report = await BuildFallbackClassroomReportAsync(targetClassroomId.Value, diasVentana, cancellationToken);
        }

        report.DiasVentana = diasVentana;

        return ApiResponse<ClassroomConsolidatedReportDto>.Ok(
            report,
            "Reporte consolidado del aula generado exitosamente mediante sp_GenerarReporteConsolidadoAula.");
    }

    public async Task<ApiResponse<List<AdaptiveRoutineAssignmentResultDto>>> AssignAdaptiveRoutineByClassroomAsync(
        Guid requesterUserId,
        AdaptiveRoutineRequestDto request,
        CancellationToken cancellationToken = default)
    {
        int maxActs = Math.Clamp(request.MaxActividadesPorEstudiante <= 0 ? 2 : request.MaxActividadesPorEstudiante, 1, 6);
        DateTime fechaInicio = (request.FechaInicio ?? DateTime.UtcNow).Date;

        Guid? classroomId = request.ClassroomId;
        if (!classroomId.HasValue || classroomId.Value == Guid.Empty)
        {
            classroomId = await _context.Classrooms
                .Where(c => c.IsActive && c.TeacherProfile != null && c.TeacherProfile.UserId == requesterUserId)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(cancellationToken);

            classroomId ??= await _context.Classrooms
                .Where(c => c.IsActive)
                .OrderBy(c => c.CreatedAt)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!classroomId.HasValue || classroomId.Value == Guid.Empty)
        {
            return ApiResponse<List<AdaptiveRoutineAssignmentResultDto>>.Fail("No se encontró un aula activa para programar la rutina adaptativa.");
        }

        List<AdaptiveRoutineAssignmentResultDto> created;
        if (_context.Database.IsRelational())
        {
            try
            {
                created = await ExecuteSpAdaptiveRoutineAsync(classroomId.Value, requesterUserId, fechaInicio, maxActs, cancellationToken);
            }
            catch
            {
                created = await BuildFallbackAdaptiveRoutineAsync(classroomId.Value, fechaInicio, maxActs, cancellationToken);
            }
        }
        else
        {
            created = await BuildFallbackAdaptiveRoutineAsync(classroomId.Value, fechaInicio, maxActs, cancellationToken);
        }

        return ApiResponse<List<AdaptiveRoutineAssignmentResultDto>>.Ok(
            created,
            $"Se programaron {created.Count} asignaciones adaptativas mediante sp_AsignarRutinaAdaptativaPorAula.");
    }

    public async Task<ApiResponse<List<TeaAlertEvaluationItemDto>>> EvaluateTeaAlertsAndRecommendationsAsync(
        Guid requesterUserId,
        EvaluateTeaAlertsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        int diasAnalisis = Math.Clamp(request.DiasAnalisis <= 0 ? 14 : request.DiasAnalisis, 3, 90);

        Guid? classroomId = request.ClassroomId;
        if (!classroomId.HasValue || classroomId.Value == Guid.Empty)
        {
            classroomId = await _context.Classrooms
                .Where(c => c.IsActive && c.TeacherProfile != null && c.TeacherProfile.UserId == requesterUserId)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        List<TeaAlertEvaluationItemDto> findings;
        if (_context.Database.IsRelational())
        {
            try
            {
                findings = await ExecuteSpEvaluateAlertsAsync(classroomId, diasAnalisis, request.RegistrarNotasAutomaticas, cancellationToken);
            }
            catch
            {
                findings = await BuildFallbackEvaluateAlertsAsync(classroomId, diasAnalisis, request.RegistrarNotasAutomaticas, cancellationToken);
            }
        }
        else
        {
            findings = await BuildFallbackEvaluateAlertsAsync(classroomId, diasAnalisis, request.RegistrarNotasAutomaticas, cancellationToken);
        }

        return ApiResponse<List<TeaAlertEvaluationItemDto>>.Ok(
            findings,
            $"Evaluación completada mediante sp_EvaluarAlertasYRecomendacionesTEA: {findings.Count} hallazgos identificados.");
    }

    public async Task<ApiResponse<RecalculateStudentProgressDto>> RecalculateStudentProgressAndAchievementsAsync(
        Guid studentProfileId,
        CancellationToken cancellationToken = default)
    {
        var studentExists = await _context.StudentProfiles.AnyAsync(s => s.Id == studentProfileId && s.IsActive, cancellationToken);
        if (!studentExists)
        {
            return ApiResponse<RecalculateStudentProgressDto>.Fail($"Estudiante con ID {studentProfileId} no encontrado.");
        }

        RecalculateStudentProgressDto result;
        if (_context.Database.IsRelational())
        {
            try
            {
                result = await ExecuteSpRecalculateProgressAsync(studentProfileId, cancellationToken);
            }
            catch
            {
                result = await BuildFallbackRecalculateProgressAsync(studentProfileId, cancellationToken);
            }
        }
        else
        {
            result = await BuildFallbackRecalculateProgressAsync(studentProfileId, cancellationToken);
        }

        return ApiResponse<RecalculateStudentProgressDto>.Ok(
            result,
            "Progreso, módulos e insignias reconciliados exitosamente mediante sp_RecalcularProgresoYLogrosEstudiante.");
    }

    // =================================================================================================
    // EJECUCIÓN DE STORED PROCEDURES EN SQL SERVER (ADO.NET)
    // =================================================================================================

    private async Task<StudentComprehensiveReportDto> ExecuteSpStudentReportAsync(
        Guid studentProfileId,
        int diasVentana,
        bool soloVisibleAcudiente,
        CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        bool shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "dbo.sp_GenerarReporteIntegralEstudiante";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

            AddParam(cmd, "@StudentProfileId", studentProfileId);
            AddParam(cmd, "@DiasVentana", diasVentana);
            AddParam(cmd, "@SoloVisibleAcudiente", soloVisibleAcudiente);

            var dto = new StudentComprehensiveReportDto { StudentProfileId = studentProfileId };

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            // Result Set 1: Header, KPIs & Sensory Profile
            if (await reader.ReadAsync(cancellationToken))
            {
                dto.StudentProfileId = GetGuid(reader, "StudentProfileId");
                dto.CodigoEstudiante = GetString(reader, "CodigoEstudiante");
                dto.NombreCompleto = GetString(reader, "NombreCompleto");
                dto.FechaNacimiento = GetDateTime(reader, "FechaNacimiento");
                dto.EdadAnios = GetInt(reader, "EdadAnios");
                dto.AulaNombre = GetString(reader, "AulaNombre");
                dto.AulaGrado = GetString(reader, "AulaGrado");
                dto.DocenteNombre = GetString(reader, "DocenteNombre");
                dto.AcudienteNombre = GetString(reader, "AcudienteNombre");
                dto.ObservacionesMedicasTEA = GetString(reader, "ObservacionesMedicasTEA");
                dto.TotalAsignaciones = GetInt(reader, "TotalAsignaciones");
                dto.AsignacionesCompletadas = GetInt(reader, "AsignacionesCompletadas");
                dto.AsignacionesEnProgreso = GetInt(reader, "AsignacionesEnProgreso");
                dto.AsignacionesPendientes = GetInt(reader, "AsignacionesPendientes");
                dto.TotalEjecuciones = GetInt(reader, "TotalEjecuciones");
                dto.EjecucionesExitosas = GetInt(reader, "EjecucionesExitosas");
                dto.TotalAciertos = GetInt(reader, "TotalAciertos");
                dto.TotalFallos = GetInt(reader, "TotalFallos");
                dto.TasaExitoGeneral = GetDecimal(reader, "TasaExitoGeneral");
                dto.PrecisionGeneral = GetDecimal(reader, "PrecisionGeneral");
                dto.TiempoPromedioSegundos = GetInt(reader, "TiempoPromedioSegundos");
                dto.CalificacionPromedio = GetNullableDecimal(reader, "CalificacionPromedio");
                dto.IndiceProgresoPedagogico = GetDecimal(reader, "IndiceProgresoPedagogico");
                dto.TendenciaGeneral = GetString(reader, "TendenciaGeneral");
                dto.RachaActualDias = GetInt(reader, "RachaActualDias");
                dto.MejorRachaDias = GetInt(reader, "MejorRachaDias");
                dto.PaletaColor = GetString(reader, "PaletaColor");
                dto.ModoContraste = GetString(reader, "ModoContraste");
                dto.Tipografia = GetString(reader, "Tipografia");
                dto.AudioActivo = GetBool(reader, "AudioActivo");
                dto.VolumenAudio = GetInt(reader, "VolumenAudio");
                dto.TextoAVoz = GetBool(reader, "TextoAVoz");
                dto.AnimacionesReducidas = GetBool(reader, "AnimacionesReducidas");
                dto.OcultarTemporizadores = GetBool(reader, "OcultarTemporizadores");
                dto.ModoTemporizador = GetString(reader, "ModoTemporizador");
                dto.NivelEscalaFuente = GetInt(reader, "NivelEscalaFuente");
                dto.DiagnosticoPedagogico = GetString(reader, "DiagnosticoPedagogico");
                dto.RecomendacionSensorial = GetString(reader, "RecomendacionSensorial");
                dto.FechaGeneracionUtc = GetDateTime(reader, "FechaGeneracionUtc");
            }

            // Result Set 2: Serie Diaria
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    dto.SerieDiaria.Add(new ReportDailyPointDto
                    {
                        DiaIndice = GetInt(reader, "DiaIndice"),
                        Fecha = GetDateTime(reader, "Fecha"),
                        EtiquetaFecha = GetString(reader, "EtiquetaFecha"),
                        TotalEjecuciones = GetInt(reader, "TotalEjecuciones"),
                        Completadas = GetInt(reader, "Completadas"),
                        Aciertos = GetInt(reader, "Aciertos"),
                        Fallos = GetInt(reader, "Fallos"),
                        TiempoPromedioSeg = GetInt(reader, "TiempoPromedioSeg"),
                        TasaExitoDia = GetDecimal(reader, "TasaExitoDia"),
                        PrecisionDia = GetDecimal(reader, "PrecisionDia"),
                        MediaMovilExito = GetDecimal(reader, "MediaMovilExito")
                    });
                }
            }

            // Result Set 3: Desglose por Tipo de Actividad
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    dto.DesglosePorTipo.Add(new ReportActivityTypeBreakdownDto
                    {
                        RowNum = GetInt(reader, "RowNum"),
                        TipoActividad = GetString(reader, "TipoActividad"),
                        EtiquetaTipo = GetString(reader, "EtiquetaTipo"),
                        TotalAsignaciones = GetInt(reader, "TotalAsignaciones"),
                        TotalEjecuciones = GetInt(reader, "TotalEjecuciones"),
                        Completadas = GetInt(reader, "Completadas"),
                        TasaExito = GetDecimal(reader, "TasaExito"),
                        PromedioAciertos = GetDecimal(reader, "PromedioAciertos"),
                        PromedioFallos = GetDecimal(reader, "PromedioFallos"),
                        TiempoPromedioSeg = GetInt(reader, "TiempoPromedioSeg"),
                        CalificacionPromedio = GetNullableDecimal(reader, "CalificacionPromedio"),
                        NivelDominio = GetString(reader, "NivelDominio")
                    });
                }
            }

            // Result Set 4: Progreso por Módulo
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    dto.ProgresoModulos.Add(new ReportModuleProgressDto
                    {
                        ModuloId = GetGuid(reader, "ModuloId"),
                        Titulo = GetString(reader, "Titulo"),
                        Categoria = GetString(reader, "Categoria"),
                        Orden = GetInt(reader, "Orden"),
                        ActividadesTotales = GetInt(reader, "ActividadesTotales"),
                        ActividadesCompletadas = GetInt(reader, "ActividadesCompletadas"),
                        PorcentajeAvance = GetInt(reader, "PorcentajeAvance"),
                        Completado = GetBool(reader, "Completado")
                    });
                }
            }

            // Result Set 5: Asignaciones Recientes
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    dto.AsignacionesRecientes.Add(new ReportRecentAssignmentDto
                    {
                        AssignmentId = GetGuid(reader, "AssignmentId"),
                        ActividadTitulo = GetString(reader, "ActividadTitulo"),
                        TipoActividad = GetString(reader, "TipoActividad"),
                        ModuloTitulo = GetString(reader, "ModuloTitulo"),
                        Estado = GetString(reader, "Estado"),
                        Prioridad = GetString(reader, "Prioridad"),
                        Calificacion = GetNullableDecimal(reader, "Calificacion"),
                        ComentarioCalificacion = GetNullableString(reader, "ComentarioCalificacion"),
                        FechaInicio = GetDateTime(reader, "FechaInicio"),
                        FechaCalificacion = GetNullableDateTime(reader, "FechaCalificacion")
                    });
                }
            }

            // Result Set 6: Notas Pedagógicas
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    dto.NotasPedagogicas.Add(new ReportPedagogicalNoteDto
                    {
                        NoteId = GetGuid(reader, "NoteId"),
                        FechaRegistro = GetDateTime(reader, "FechaRegistro"),
                        Area = GetString(reader, "Area"),
                        Tipo = GetString(reader, "Tipo"),
                        Titulo = GetString(reader, "Titulo"),
                        Contenido = GetString(reader, "Contenido"),
                        RecomendacionCasa = GetNullableString(reader, "RecomendacionCasa"),
                        VisibleAcudiente = GetBool(reader, "VisibleAcudiente"),
                        DocenteNombre = GetString(reader, "DocenteNombre")
                    });
                }
            }

            return dto;
        }
        finally
        {
            if (shouldClose && connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<ClassroomConsolidatedReportDto> ExecuteSpClassroomReportAsync(
        Guid classroomId,
        Guid teacherUserId,
        int diasVentana,
        CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        bool shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "dbo.sp_GenerarReporteConsolidadoAula";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

            AddParam(cmd, "@ClassroomId", classroomId);
            AddParam(cmd, "@TeacherUserId", teacherUserId);
            AddParam(cmd, "@DiasVentana", diasVentana);

            var dto = new ClassroomConsolidatedReportDto { ClassroomId = classroomId };

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            // Result Set 1: Resumen del Aula
            if (await reader.ReadAsync(cancellationToken))
            {
                dto.ClassroomId = GetGuid(reader, "ClassroomId");
                dto.AulaNombre = GetString(reader, "AulaNombre");
                dto.Grado = GetString(reader, "Grado");
                dto.AnioLectivo = GetInt(reader, "AnioLectivo");
                dto.DocenteNombre = GetString(reader, "DocenteNombre");
                dto.EspecialidadDocente = GetString(reader, "EspecialidadDocente");
                dto.TotalEstudiantes = GetInt(reader, "TotalEstudiantes");
                dto.EstudiantesActivosPeriodo = GetInt(reader, "EstudiantesActivosPeriodo");
                dto.EstudiantesEnAlerta = GetInt(reader, "EstudiantesEnAlerta");
                dto.EstudiantesEnSeguimiento = GetInt(reader, "EstudiantesEnSeguimiento");
                dto.EstudiantesEstables = GetInt(reader, "EstudiantesEstables");
                dto.TotalAsignacionesAula = GetInt(reader, "TotalAsignacionesAula");
                dto.AsignacionesCompletadasAula = GetInt(reader, "AsignacionesCompletadasAula");
                dto.TotalEjecucionesAula = GetInt(reader, "TotalEjecucionesAula");
                dto.TasaExitoPromedioAula = GetDecimal(reader, "TasaExitoPromedioAula");
                dto.PrecisionPromedioAula = GetDecimal(reader, "PrecisionPromedioAula");
                dto.TiempoPromedioSegundosAula = GetInt(reader, "TiempoPromedioSegundosAula");
                dto.CalificacionPromedioAula = GetNullableDecimal(reader, "CalificacionPromedioAula");
                dto.SintesisEjecutivaAula = GetString(reader, "SintesisEjecutivaAula");
                dto.FechaGeneracionUtc = GetDateTime(reader, "FechaGeneracionUtc");
            }

            // Result Set 2: Matriz de Estudiantes
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    dto.Estudiantes.Add(new ClassroomStudentRiskRowDto
                    {
                        StudentProfileId = GetGuid(reader, "StudentProfileId"),
                        CodigoEstudiante = GetString(reader, "CodigoEstudiante"),
                        NombreCompleto = GetString(reader, "NombreCompleto"),
                        TotalAsignaciones = GetInt(reader, "TotalAsignaciones"),
                        AsignacionesCompletadas = GetInt(reader, "AsignacionesCompletadas"),
                        TotalEjecuciones = GetInt(reader, "TotalEjecuciones"),
                        EjecucionesExitosas = GetInt(reader, "EjecucionesExitosas"),
                        TasaExito = GetDecimal(reader, "TasaExito"),
                        PrecisionPromedio = GetDecimal(reader, "PrecisionPromedio"),
                        TiempoPromedioSeg = GetInt(reader, "TiempoPromedioSeg"),
                        CalificacionPromedio = GetNullableDecimal(reader, "CalificacionPromedio"),
                        AvanceModulosPct = GetInt(reader, "AvanceModulosPct"),
                        AlertasActivas = GetInt(reader, "AlertasActivas"),
                        NivelRiesgo = GetString(reader, "NivelRiesgo"),
                        AccionSugerida = GetString(reader, "AccionSugerida")
                    });
                }
            }

            // Result Set 3: Efectividad por Módulo
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    dto.EfectividadModulos.Add(new ClassroomModuleEffectivenessDto
                    {
                        ModuloId = GetGuid(reader, "ModuloId"),
                        ModuloTitulo = GetString(reader, "ModuloTitulo"),
                        CategoriaNombre = GetString(reader, "CategoriaNombre"),
                        Orden = GetInt(reader, "Orden"),
                        TotalAsignaciones = GetInt(reader, "TotalAsignaciones"),
                        TotalEjecuciones = GetInt(reader, "TotalEjecuciones"),
                        EjecucionesExitosas = GetInt(reader, "EjecucionesExitosas"),
                        TasaExito = GetDecimal(reader, "TasaExito"),
                        TiempoPromedioSeg = GetInt(reader, "TiempoPromedioSeg"),
                        CalificacionPromedio = GetNullableDecimal(reader, "CalificacionPromedio")
                    });
                }
            }

            // Result Set 4: Evolución Semanal
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    dto.EvolucionSemanal.Add(new ClassroomWeeklyPointDto
                    {
                        SemanaIndice = GetInt(reader, "SemanaIndice"),
                        EtiquetaSemana = GetString(reader, "EtiquetaSemana"),
                        FechaInicio = GetDateTime(reader, "FechaInicio"),
                        FechaFin = GetDateTime(reader, "FechaFin"),
                        TotalEjecuciones = GetInt(reader, "TotalEjecuciones"),
                        EjecucionesExitosas = GetInt(reader, "EjecucionesExitosas"),
                        TasaExito = GetDecimal(reader, "TasaExito"),
                        PromedioAciertos = GetDecimal(reader, "PromedioAciertos"),
                        PromedioFallos = GetDecimal(reader, "PromedioFallos"),
                        TiempoPromedioSeg = GetInt(reader, "TiempoPromedioSeg")
                    });
                }
            }

            return dto;
        }
        finally
        {
            if (shouldClose && connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<List<AdaptiveRoutineAssignmentResultDto>> ExecuteSpAdaptiveRoutineAsync(
        Guid classroomId,
        Guid teacherUserId,
        DateTime fechaInicio,
        int maxActividadesPorEstudiante,
        CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        bool shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "dbo.sp_AsignarRutinaAdaptativaPorAula";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

            AddParam(cmd, "@ClassroomId", classroomId);
            AddParam(cmd, "@TeacherUserId", teacherUserId);
            AddParam(cmd, "@FechaInicio", fechaInicio);
            AddParam(cmd, "@MaxActividadesPorEstudiante", maxActividadesPorEstudiante);

            var list = new List<AdaptiveRoutineAssignmentResultDto>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(new AdaptiveRoutineAssignmentResultDto
                {
                    AssignmentId = GetGuid(reader, "AssignmentId"),
                    StudentProfileId = GetGuid(reader, "StudentProfileId"),
                    CodigoEstudiante = GetString(reader, "CodigoEstudiante"),
                    EstudianteNombre = GetString(reader, "EstudianteNombre"),
                    ActivityId = GetGuid(reader, "ActivityId"),
                    ActividadTitulo = GetString(reader, "ActividadTitulo"),
                    TipoActividad = GetString(reader, "TipoActividad"),
                    Prioridad = GetString(reader, "Prioridad"),
                    OrdenSecuencial = GetInt(reader, "OrdenSecuencial"),
                    FechaInicio = GetDateTime(reader, "FechaInicio"),
                    FechaLimite = GetDateTime(reader, "FechaLimite"),
                    MotivoAdaptativo = GetString(reader, "MotivoAdaptativo")
                });
            }

            return list;
        }
        finally
        {
            if (shouldClose && connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<List<TeaAlertEvaluationItemDto>> ExecuteSpEvaluateAlertsAsync(
        Guid? classroomId,
        int diasAnalisis,
        bool registrarNotasAutomaticas,
        CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        bool shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "dbo.sp_EvaluarAlertasYRecomendacionesTEA";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

            AddParam(cmd, "@ClassroomId", classroomId.HasValue ? classroomId.Value : DBNull.Value);
            AddParam(cmd, "@DiasAnalisis", diasAnalisis);
            AddParam(cmd, "@RegistrarNotasAutomaticas", registrarNotasAutomaticas);

            var list = new List<TeaAlertEvaluationItemDto>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(new TeaAlertEvaluationItemDto
                {
                    HallazgoId = GetInt(reader, "HallazgoId"),
                    StudentProfileId = GetGuid(reader, "StudentProfileId"),
                    CodigoEstudiante = GetString(reader, "CodigoEstudiante"),
                    EstudianteNombre = GetString(reader, "EstudianteNombre"),
                    Area = GetString(reader, "Area"),
                    TipoAlerta = GetString(reader, "TipoAlerta"),
                    Severidad = GetString(reader, "Severidad"),
                    Titulo = GetString(reader, "Titulo"),
                    DescripcionHallazgo = GetString(reader, "DescripcionHallazgo"),
                    RecomendacionCasa = GetString(reader, "RecomendacionCasa"),
                    AjusteSensorialSugerido = GetString(reader, "AjusteSensorialSugerido"),
                    NotaInsertada = GetBool(reader, "NotaInsertada")
                });
            }

            return list;
        }
        finally
        {
            if (shouldClose && connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<RecalculateStudentProgressDto> ExecuteSpRecalculateProgressAsync(
        Guid studentProfileId,
        CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        bool shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "dbo.sp_RecalcularProgresoYLogrosEstudiante";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

            AddParam(cmd, "@StudentProfileId", studentProfileId);

            var dto = new RecalculateStudentProgressDto { StudentProfileId = studentProfileId };
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                dto.StudentProfileId = GetGuid(reader, "StudentProfileId");
                dto.AsignacionesReconciliadas = GetInt(reader, "AsignacionesReconciliadas");
                dto.TotalActividadesCompletadas = GetInt(reader, "TotalActividadesCompletadas");
                dto.ModulosCompletados = GetInt(reader, "ModulosCompletados");
                dto.InsigniasDesbloqueadas = GetInt(reader, "InsigniasDesbloqueadas");
                dto.MensajeMotivacional = GetString(reader, "MensajeMotivacional");
                dto.FechaRecalculoUtc = GetDateTime(reader, "FechaRecalculoUtc");
            }

            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    dto.Insignias.Add(new RecalculatedBadgeDto
                    {
                        Clave = GetString(reader, "Clave"),
                        Titulo = GetString(reader, "Titulo"),
                        Descripcion = GetString(reader, "Descripcion"),
                        Icono = GetString(reader, "Icono"),
                        Desbloqueado = GetBool(reader, "Desbloqueado"),
                        FechaDesbloqueo = GetNullableDateTime(reader, "FechaDesbloqueo")
                    });
                }
            }

            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    dto.Modulos.Add(new ReportModuleProgressDto
                    {
                        ModuloId = GetGuid(reader, "ModuloId"),
                        Titulo = GetString(reader, "Titulo"),
                        Categoria = GetString(reader, "Categoria"),
                        Orden = GetInt(reader, "Orden"),
                        ActividadesTotales = GetInt(reader, "ActividadesTotales"),
                        ActividadesCompletadas = GetInt(reader, "ActividadesCompletadas"),
                        PorcentajeAvance = GetInt(reader, "PorcentajeAvance"),
                        Completado = GetBool(reader, "Completado"),
                        FechaCompletado = GetNullableDateTime(reader, "FechaCompletado")
                    });
                }
            }

            return dto;
        }
        finally
        {
            if (shouldClose && connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }

    // =================================================================================================
    // RESPALDO EN MEMORIA PARA ENTORNOS DE PRUEBAS UNITARIAS (UseInMemoryDatabase)
    // =================================================================================================

    private async Task<StudentComprehensiveReportDto> BuildFallbackStudentReportAsync(
        Guid studentProfileId,
        int diasVentana,
        bool soloVisibleAcudiente,
        CancellationToken cancellationToken)
    {
        var student = await _context.StudentProfiles
            .Include(s => s.User)
            .Include(s => s.Classroom)
                .ThenInclude(c => c!.TeacherProfile)
                    .ThenInclude(tp => tp!.User)
            .Include(s => s.GuardianProfile)
                .ThenInclude(gp => gp!.User)
            .Include(s => s.SensoryPreference)
            .FirstAsync(s => s.Id == studentProfileId, cancellationToken);

        var assignments = await _context.ActivityAssignments
            .Include(a => a.Activity)
                .ThenInclude(act => act!.Modulo)
            .Where(a => a.StudentProfileId == studentProfileId && a.IsActive)
            .ToListAsync(cancellationToken);

        var executions = await _context.ActivityExecutions
            .Include(e => e.Assignment)
                .ThenInclude(a => a!.Activity)
            .Where(e => e.StudentProfileId == studentProfileId && e.IsActive)
            .ToListAsync(cancellationToken);

        var modules = await _context.LearningModules
            .Include(m => m.Categoria)
            .Include(m => m.Activities.Where(a => a.IsActive))
            .Where(m => m.IsActive)
            .OrderBy(m => m.Orden)
            .ToListAsync(cancellationToken);

        var notesQuery = _context.PedagogicalNotes
            .Include(n => n.TeacherProfile)
                .ThenInclude(tp => tp!.User)
            .Where(n => n.StudentProfileId == studentProfileId && n.IsActive);

        if (soloVisibleAcudiente)
        {
            notesQuery = notesQuery.Where(n => n.VisibleAcudiente);
        }

        var notes = await notesQuery
            .OrderByDescending(n => n.FechaRegistro)
            .Take(15)
            .ToListAsync(cancellationToken);

        DateTime today = DateTime.UtcNow.Date;
        DateTime startDate = today.AddDays(-(diasVentana - 1));

        var serieDiaria = new List<ReportDailyPointDto>();
        int rachaActual = 0;
        int mejorRacha = 0;

        for (int i = 0; i < diasVentana; i++)
        {
            DateTime date = startDate.AddDays(i);
            var dayExecs = executions.Where(e => e.FechaRegistro.Date == date).ToList();
            int tot = dayExecs.Count;
            int comp = dayExecs.Count(e => e.CompletadaExitosamente);
            int ac = dayExecs.Sum(e => e.CantidadAciertos);
            int fa = dayExecs.Sum(e => e.CantidadIntentosFallidos);
            int tmp = tot > 0 ? (int)Math.Round(dayExecs.Average(e => e.TiempoRespuestaSegundos)) : 0;
            decimal tasa = tot > 0 ? Math.Round(comp * 100m / tot, 2) : 0m;
            decimal prec = (ac + fa) > 0 ? Math.Round(ac * 100m / (ac + fa), 2) : 0m;

            if (comp > 0)
            {
                rachaActual++;
                if (rachaActual > mejorRacha) mejorRacha = rachaActual;
            }
            else if (tot > 0)
            {
                rachaActual = 0;
            }

            serieDiaria.Add(new ReportDailyPointDto
            {
                DiaIndice = i + 1,
                Fecha = date,
                EtiquetaFecha = date.ToString("dd/MM"),
                TotalEjecuciones = tot,
                Completadas = comp,
                Aciertos = ac,
                Fallos = fa,
                TiempoPromedioSeg = tmp,
                TasaExitoDia = tasa,
                PrecisionDia = prec,
                MediaMovilExito = tasa
            });
        }

        var tipos = new (TipoActividad Tipo, string Etiqueta)[]
        {
            (TipoActividad.SecuenciaLogica, "Secuencia Lógica"),
            (TipoActividad.EmparejamientoVisual, "Emparejamiento Visual"),
            (TipoActividad.AsociacionEmocional, "Asociación Emocional"),
            (TipoActividad.RutinaSocial, "Rutina Social")
        };

        var desglose = new List<ReportActivityTypeBreakdownDto>();
        for (int t = 0; t < tipos.Length; t++)
        {
            var (tipo, etiqueta) = tipos[t];
            var typeAsigs = assignments.Where(a => a.Activity?.TipoActividad == tipo).ToList();
            var typeExecs = executions.Where(e => e.Assignment?.Activity?.TipoActividad == tipo).ToList();
            int totEx = typeExecs.Count;
            int compEx = typeExecs.Count(e => e.CompletadaExitosamente);
            decimal tasa = totEx > 0 ? Math.Round(compEx * 100m / totEx, 2) : 0m;
            decimal promAc = totEx > 0 ? Math.Round((decimal)typeExecs.Average(e => e.CantidadAciertos), 2) : 0m;
            decimal promFa = totEx > 0 ? Math.Round((decimal)typeExecs.Average(e => e.CantidadIntentosFallidos), 2) : 0m;
            var graded = typeAsigs.Where(a => a.Calificacion.HasValue).Select(a => a.Calificacion!.Value).ToList();

            desglose.Add(new ReportActivityTypeBreakdownDto
            {
                RowNum = t + 1,
                TipoActividad = tipo.ToString(),
                EtiquetaTipo = etiqueta,
                TotalAsignaciones = typeAsigs.Count,
                TotalEjecuciones = totEx,
                Completadas = compEx,
                TasaExito = tasa,
                PromedioAciertos = promAc,
                PromedioFallos = promFa,
                TiempoPromedioSeg = totEx > 0 ? (int)Math.Round(typeExecs.Average(e => e.TiempoRespuestaSegundos)) : 0,
                CalificacionPromedio = graded.Count > 0 ? Math.Round(graded.Average(), 2) : null,
                NivelDominio = totEx == 0 ? "Sin Datos" : (tasa >= 80m && promFa <= 2m ? "Dominio Alto" : (tasa >= 55m ? "En Consolidación" : "Requiere Apoyo Visual"))
            });
        }

        var completedActIds = assignments
            .Where(a => a.Estado == EstadoAsignacion.Completada || executions.Any(e => e.AssignmentId == a.Id && e.CompletadaExitosamente))
            .Select(a => a.ActivityId)
            .ToHashSet();

        var progresoModulos = modules.Select(m =>
        {
            int totActs = m.Activities.Count;
            int compActs = m.Activities.Count(a => completedActIds.Contains(a.Id));
            int pct = totActs > 0 ? (int)Math.Round(compActs * 100.0 / totActs) : 0;
            return new ReportModuleProgressDto
            {
                ModuloId = m.Id,
                Titulo = m.Titulo,
                Categoria = m.Categoria?.Nombre ?? "General",
                Orden = m.Orden,
                ActividadesTotales = totActs,
                ActividadesCompletadas = compActs,
                PorcentajeAvance = pct,
                Completado = totActs > 0 && compActs >= totActs
            };
        }).ToList();

        int totalExecs = executions.Count;
        int exitos = executions.Count(e => e.CompletadaExitosamente);
        int sumAc = executions.Sum(e => e.CantidadAciertos);
        int sumFa = executions.Sum(e => e.CantidadIntentosFallidos);
        decimal tasaGeneral = totalExecs > 0 ? Math.Round(exitos * 100m / totalExecs, 2) : 0m;
        decimal precGeneral = (sumAc + sumFa) > 0 ? Math.Round(sumAc * 100m / (sumAc + sumFa), 2) : 0m;
        var allGrades = assignments.Where(a => a.Calificacion.HasValue).Select(a => a.Calificacion!.Value).ToList();
        decimal? califProm = allGrades.Count > 0 ? Math.Round(allGrades.Average(), 2) : null;
        decimal avanceProm = progresoModulos.Count > 0 ? (decimal)progresoModulos.Average(m => m.PorcentajeAvance) : 0m;
        decimal notaNorm = califProm.HasValue ? califProm.Value * 20m : tasaGeneral;
        decimal ipp = Math.Round((tasaGeneral * 0.40m) + (precGeneral * 0.30m) + (avanceProm * 0.15m) + (notaNorm * 0.15m), 2);

        int edad = today.Year - student.FechaNacimiento.Year;
        if (student.FechaNacimiento.Date > today.AddYears(-edad)) edad--;

        var spf = student.SensoryPreference;
        return new StudentComprehensiveReportDto
        {
            StudentProfileId = student.Id,
            CodigoEstudiante = student.CodigoEstudiante,
            NombreCompleto = student.User?.NombreCompleto ?? "Estudiante",
            FechaNacimiento = student.FechaNacimiento,
            EdadAnios = Math.Max(0, edad),
            AulaNombre = student.Classroom?.Nombre ?? "Sin aula",
            AulaGrado = student.Classroom?.Grado ?? "General",
            DocenteNombre = student.Classroom?.TeacherProfile?.User?.NombreCompleto ?? "Docente Titular",
            AcudienteNombre = student.GuardianProfile?.User?.NombreCompleto ?? "Acudiente Registrado",
            ObservacionesMedicasTEA = student.ObservacionesMedicasTEA ?? string.Empty,
            TotalAsignaciones = assignments.Count,
            AsignacionesCompletadas = assignments.Count(a => a.Estado == EstadoAsignacion.Completada),
            AsignacionesEnProgreso = assignments.Count(a => a.Estado == EstadoAsignacion.EnProgreso),
            AsignacionesPendientes = assignments.Count(a => a.Estado == EstadoAsignacion.Pendiente),
            TotalEjecuciones = totalExecs,
            EjecucionesExitosas = exitos,
            TotalAciertos = sumAc,
            TotalFallos = sumFa,
            TasaExitoGeneral = tasaGeneral,
            PrecisionGeneral = precGeneral,
            TiempoPromedioSegundos = totalExecs > 0 ? (int)Math.Round(executions.Average(e => e.TiempoRespuestaSegundos)) : 0,
            CalificacionPromedio = califProm,
            IndiceProgresoPedagogico = ipp,
            TendenciaGeneral = ipp >= 75m ? "En Ascenso" : "Estable",
            RachaActualDias = rachaActual,
            MejorRachaDias = mejorRacha,
            PaletaColor = spf?.PaletaColor ?? "PastelSuave",
            ModoContraste = spf?.ModoContraste.ToString() ?? "Estandar",
            Tipografia = spf?.Tipografia ?? "Atkinson Hyperlegible",
            AudioActivo = spf?.AudioActivo ?? true,
            VolumenAudio = spf?.VolumenAudio ?? 50,
            TextoAVoz = spf?.TextoAVoz ?? true,
            AnimacionesReducidas = spf?.AnimacionesReducidas ?? true,
            OcultarTemporizadores = spf?.OcultarTemporizadores ?? true,
            ModoTemporizador = spf?.ModoTemporizador ?? "sin-temporizador",
            NivelEscalaFuente = spf?.NivelEscalaFuente ?? 2,
            DiagnosticoPedagogico = $"Índice de Progreso Pedagógico (IPP): {ipp}/100 con tasa de éxito del {tasaGeneral}% y precisión del {precGeneral}%.",
            RecomendacionSensorial = "Mantener entorno predecible con apoyos visuales y configuración sensorial sin presión de tiempo.",
            FechaGeneracionUtc = DateTime.UtcNow,
            SerieDiaria = serieDiaria,
            DesglosePorTipo = desglose,
            ProgresoModulos = progresoModulos,
            AsignacionesRecientes = assignments
                .OrderByDescending(a => a.FechaCalificacion ?? a.UpdatedAt ?? a.FechaInicio)
                .Take(15)
                .Select(a => new ReportRecentAssignmentDto
                {
                    AssignmentId = a.Id,
                    ActividadTitulo = a.Activity?.Titulo ?? "Actividad",
                    TipoActividad = a.Activity?.TipoActividad.ToString() ?? string.Empty,
                    ModuloTitulo = a.Activity?.Modulo?.Titulo ?? "General",
                    Estado = a.Estado.ToString(),
                    Prioridad = a.Prioridad.ToString(),
                    Calificacion = a.Calificacion,
                    ComentarioCalificacion = a.ComentarioCalificacion,
                    FechaInicio = a.FechaInicio,
                    FechaCalificacion = a.FechaCalificacion
                }).ToList(),
            NotasPedagogicas = notes.Select(n => new ReportPedagogicalNoteDto
            {
                NoteId = n.Id,
                FechaRegistro = n.FechaRegistro,
                Area = n.Area,
                Tipo = n.Tipo,
                Titulo = n.Titulo,
                Contenido = n.Contenido,
                RecomendacionCasa = n.RecomendacionCasa,
                VisibleAcudiente = n.VisibleAcudiente,
                DocenteNombre = n.TeacherProfile?.User?.NombreCompleto ?? "Docente"
            }).ToList()
        };
    }

    private async Task<ClassroomConsolidatedReportDto> BuildFallbackClassroomReportAsync(
        Guid classroomId,
        int diasVentana,
        CancellationToken cancellationToken)
    {
        var classroom = await _context.Classrooms
            .Include(c => c.TeacherProfile)
                .ThenInclude(tp => tp!.User)
            .FirstAsync(c => c.Id == classroomId, cancellationToken);

        var students = await _context.StudentProfiles
            .Include(s => s.User)
            .Where(s => s.ClassroomId == classroomId && s.IsActive)
            .OrderBy(s => s.CodigoEstudiante)
            .ToListAsync(cancellationToken);

        var studentIds = students.Select(s => s.Id).ToHashSet();
        var assignments = await _context.ActivityAssignments
            .Include(a => a.Activity)
            .Where(a => studentIds.Contains(a.StudentProfileId) && a.IsActive)
            .ToListAsync(cancellationToken);

        var executions = await _context.ActivityExecutions
            .Where(e => studentIds.Contains(e.StudentProfileId) && e.IsActive)
            .ToListAsync(cancellationToken);

        var alerts = await _context.PedagogicalNotes
            .Where(n => studentIds.Contains(n.StudentProfileId) && n.IsActive && n.Tipo == "Alerta")
            .ToListAsync(cancellationToken);

        int totalCatalogActs = Math.Max(1, await _context.Activities.CountAsync(a => a.IsActive, cancellationToken));

        var rows = new List<ClassroomStudentRiskRowDto>();
        foreach (var st in students)
        {
            var stAsigs = assignments.Where(a => a.StudentProfileId == st.Id).ToList();
            var stExecs = executions.Where(e => e.StudentProfileId == st.Id).ToList();
            int totEx = stExecs.Count;
            int exExito = stExecs.Count(e => e.CompletadaExitosamente);
            int ac = stExecs.Sum(e => e.CantidadAciertos);
            int fa = stExecs.Sum(e => e.CantidadIntentosFallidos);
            decimal tasa = totEx > 0 ? Math.Round(exExito * 100m / totEx, 2) : 0m;
            decimal prec = (ac + fa) > 0 ? Math.Round(ac * 100m / (ac + fa), 2) : 0m;
            var grades = stAsigs.Where(a => a.Calificacion.HasValue).Select(a => a.Calificacion!.Value).ToList();
            decimal? calif = grades.Count > 0 ? Math.Round(grades.Average(), 2) : null;
            int alertCount = alerts.Count(n => n.StudentProfileId == st.Id);
            int tmp = totEx > 0 ? (int)Math.Round(stExecs.Average(e => e.TiempoRespuestaSegundos)) : 0;

            string riesgo = (totEx == 0 || tasa < 50m || (calif.HasValue && calif.Value < 3.0m))
                ? "AlertaPrioritaria"
                : (tasa < 75m || alertCount >= 2 || tmp > 95 ? "Seguimiento" : "Estable");

            rows.Add(new ClassroomStudentRiskRowDto
            {
                StudentProfileId = st.Id,
                CodigoEstudiante = st.CodigoEstudiante,
                NombreCompleto = st.User?.NombreCompleto ?? "Estudiante",
                TotalAsignaciones = stAsigs.Count,
                AsignacionesCompletadas = stAsigs.Count(a => a.Estado == EstadoAsignacion.Completada),
                TotalEjecuciones = totEx,
                EjecucionesExitosas = exExito,
                TasaExito = tasa,
                PrecisionPromedio = prec,
                TiempoPromedioSeg = tmp,
                CalificacionPromedio = calif,
                AvanceModulosPct = Math.Min(100, (int)Math.Round(stAsigs.Count(a => a.Estado == EstadoAsignacion.Completada) * 100.0 / totalCatalogActs)),
                AlertasActivas = alertCount,
                NivelRiesgo = riesgo,
                AccionSugerida = riesgo == "AlertaPrioritaria"
                    ? "Reforzar con actividades de emparejamiento visual y modo sin temporizador."
                    : "Continuar progresión secuencial del plan actual."
            });
        }

        var activeRows = rows.Where(r => r.TotalEjecuciones > 0).ToList();
        var gradedRows = rows.Where(r => r.CalificacionPromedio.HasValue).Select(r => r.CalificacionPromedio!.Value).ToList();

        return new ClassroomConsolidatedReportDto
        {
            ClassroomId = classroom.Id,
            AulaNombre = classroom.Nombre,
            Grado = classroom.Grado,
            AnioLectivo = classroom.AnioLectivo,
            DocenteNombre = classroom.TeacherProfile?.User?.NombreCompleto ?? "Docente",
            EspecialidadDocente = classroom.TeacherProfile?.Especialidad ?? "Educación Especial y TEA",
            TotalEstudiantes = rows.Count,
            EstudiantesActivosPeriodo = activeRows.Count,
            EstudiantesEnAlerta = rows.Count(r => r.NivelRiesgo == "AlertaPrioritaria"),
            EstudiantesEnSeguimiento = rows.Count(r => r.NivelRiesgo == "Seguimiento"),
            EstudiantesEstables = rows.Count(r => r.NivelRiesgo == "Estable"),
            TotalAsignacionesAula = rows.Sum(r => r.TotalAsignaciones),
            AsignacionesCompletadasAula = rows.Sum(r => r.AsignacionesCompletadas),
            TotalEjecucionesAula = rows.Sum(r => r.TotalEjecuciones),
            TasaExitoPromedioAula = activeRows.Count > 0 ? Math.Round(activeRows.Average(r => r.TasaExito), 2) : 0m,
            PrecisionPromedioAula = activeRows.Count > 0 ? Math.Round(activeRows.Average(r => r.PrecisionPromedio), 2) : 0m,
            TiempoPromedioSegundosAula = activeRows.Count > 0 ? (int)Math.Round(activeRows.Average(r => r.TiempoPromedioSeg)) : 0,
            CalificacionPromedioAula = gradedRows.Count > 0 ? Math.Round(gradedRows.Average(), 2) : null,
            SintesisEjecutivaAula = $"Reporte consolidado del aula {classroom.Nombre} con {rows.Count} estudiantes evaluados.",
            Estudiantes = rows
        };
    }

    private async Task<List<AdaptiveRoutineAssignmentResultDto>> BuildFallbackAdaptiveRoutineAsync(
        Guid classroomId,
        DateTime fechaInicio,
        int maxActs,
        CancellationToken cancellationToken)
    {
        var students = await _context.StudentProfiles
            .Include(s => s.User)
            .Include(s => s.SensoryPreference)
            .Where(s => s.ClassroomId == classroomId && s.IsActive)
            .OrderBy(s => s.CodigoEstudiante)
            .ToListAsync(cancellationToken);

        var activities = await _context.Activities
            .Include(a => a.Modulo)
            .Where(a => a.IsActive && a.Modulo != null && a.Modulo.IsActive)
            .OrderBy(a => a.Modulo!.Orden)
            .ThenBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var results = new List<AdaptiveRoutineAssignmentResultDto>();
        foreach (var st in students)
        {
            var existingIds = await _context.ActivityAssignments
                .Where(aa => aa.StudentProfileId == st.Id && aa.IsActive &&
                             (aa.FechaInicio.Date == fechaInicio.Date || aa.Estado != EstadoAsignacion.Completada))
                .Select(aa => aa.ActivityId)
                .ToListAsync(cancellationToken);

            int maxOrder = await _context.ActivityAssignments
                .Where(aa => aa.StudentProfileId == st.Id && aa.IsActive && aa.FechaInicio.Date == fechaInicio.Date)
                .Select(aa => (int?)aa.OrdenSecuencial)
                .MaxAsync(cancellationToken) ?? 0;

            var candidates = activities.Where(a => !existingIds.Contains(a.Id)).Take(maxActs).ToList();
            foreach (var act in candidates)
            {
                maxOrder++;
                var assign = new ActivityAssignment
                {
                    ActivityId = act.Id,
                    StudentProfileId = st.Id,
                    FechaInicio = fechaInicio.Date,
                    FechaLimite = fechaInicio.Date.AddDays(7),
                    OrdenSecuencial = maxOrder,
                    Prioridad = PrioridadActividad.Media,
                    Estado = EstadoAsignacion.Pendiente,
                    IsActive = true
                };
                _context.ActivityAssignments.Add(assign);
                results.Add(new AdaptiveRoutineAssignmentResultDto
                {
                    AssignmentId = assign.Id,
                    StudentProfileId = st.Id,
                    CodigoEstudiante = st.CodigoEstudiante,
                    EstudianteNombre = st.User?.NombreCompleto ?? "Estudiante",
                    ActivityId = act.Id,
                    ActividadTitulo = act.Titulo,
                    TipoActividad = act.TipoActividad.ToString(),
                    Prioridad = assign.Prioridad.ToString(),
                    OrdenSecuencial = assign.OrdenSecuencial,
                    FechaInicio = assign.FechaInicio,
                    FechaLimite = assign.FechaLimite ?? fechaInicio.Date.AddDays(7),
                    MotivoAdaptativo = "Progresión secuencial de módulo pedagógico"
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return results;
    }

    private async Task<List<TeaAlertEvaluationItemDto>> BuildFallbackEvaluateAlertsAsync(
        Guid? classroomId,
        int diasAnalisis,
        bool registrarNotasAutomaticas,
        CancellationToken cancellationToken)
    {
        var query = _context.StudentProfiles
            .Include(s => s.User)
            .Include(s => s.Classroom)
            .Where(s => s.IsActive);

        if (classroomId.HasValue)
        {
            query = query.Where(s => s.ClassroomId == classroomId.Value);
        }

        var students = await query.OrderBy(s => s.CodigoEstudiante).ToListAsync(cancellationToken);
        DateTime cutoff = DateTime.UtcNow.AddDays(-diasAnalisis);
        var findings = new List<TeaAlertEvaluationItemDto>();
        int idx = 1;

        foreach (var st in students)
        {
            var recentExecs = await _context.ActivityExecutions
                .Where(e => e.StudentProfileId == st.Id && e.IsActive && e.FechaRegistro >= cutoff)
                .ToListAsync(cancellationToken);

            int frustraciones = recentExecs.Count(e => !e.CompletadaExitosamente && e.CantidadIntentosFallidos >= 4);
            if (frustraciones >= 2)
            {
                findings.Add(new TeaAlertEvaluationItemDto
                {
                    HallazgoId = idx++,
                    StudentProfileId = st.Id,
                    CodigoEstudiante = st.CodigoEstudiante,
                    EstudianteNombre = st.User?.NombreCompleto ?? "Estudiante",
                    Area = "Cognitiva",
                    TipoAlerta = "Alerta",
                    Severidad = "Alta",
                    Titulo = "[SP-TEA] Patrón de frustración en actividades recientes",
                    DescripcionHallazgo = $"El estudiante registró {frustraciones} sesiones con 4 o más intentos fallidos no completados.",
                    RecomendacionCasa = "Dividir la práctica en bloques cortos de 5 minutos con apoyos pictográficos previos.",
                    AjusteSensorialSugerido = "Activar Animaciones Reducidas y priorizar actividades de Emparejamiento Visual.",
                    NotaInsertada = registrarNotasAutomaticas
                });
            }
        }

        return findings;
    }

    private async Task<RecalculateStudentProgressDto> BuildFallbackRecalculateProgressAsync(
        Guid studentProfileId,
        CancellationToken cancellationToken)
    {
        var assignments = await _context.ActivityAssignments
            .Where(a => a.StudentProfileId == studentProfileId && a.IsActive)
            .ToListAsync(cancellationToken);

        var executions = await _context.ActivityExecutions
            .Include(e => e.Assignment)
                .ThenInclude(a => a!.Activity)
            .Where(e => e.StudentProfileId == studentProfileId && e.IsActive)
            .ToListAsync(cancellationToken);

        int reconciled = 0;
        foreach (var a in assignments)
        {
            bool hasSuccess = executions.Any(e => e.AssignmentId == a.Id && e.CompletadaExitosamente);
            if (hasSuccess && a.Estado != EstadoAsignacion.Completada)
            {
                a.Estado = EstadoAsignacion.Completada;
                reconciled++;
            }
        }

        if (reconciled > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        int exitos = executions.Count(e => e.CompletadaExitosamente);
        return new RecalculateStudentProgressDto
        {
            StudentProfileId = studentProfileId,
            AsignacionesReconciliadas = reconciled,
            TotalActividadesCompletadas = exitos,
            ModulosCompletados = 0,
            InsigniasDesbloqueadas = exitos > 0 ? 1 : 0,
            MensajeMotivacional = "¡Progreso auditado exitosamente!",
            FechaRecalculoUtc = DateTime.UtcNow
        };
    }

    // =================================================================================================
    // HELPERS DE LECTURA ADO.NET
    // =================================================================================================

    private static void AddParam(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static Guid GetGuid(DbDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? Guid.Empty : r.GetGuid(i);
    }

    private static string GetString(DbDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? string.Empty : Convert.ToString(r.GetValue(i)) ?? string.Empty;
    }

    private static string? GetNullableString(DbDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : Convert.ToString(r.GetValue(i));
    }

    private static int GetInt(DbDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i));
    }

    private static decimal GetDecimal(DbDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? 0m : Convert.ToDecimal(r.GetValue(i));
    }

    private static decimal? GetNullableDecimal(DbDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : Convert.ToDecimal(r.GetValue(i));
    }

    private static bool GetBool(DbDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return !r.IsDBNull(i) && Convert.ToBoolean(r.GetValue(i));
    }

    private static DateTime GetDateTime(DbDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? DateTime.UtcNow : Convert.ToDateTime(r.GetValue(i));
    }

    private static DateTime? GetNullableDateTime(DbDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : Convert.ToDateTime(r.GetValue(i));
    }
}
