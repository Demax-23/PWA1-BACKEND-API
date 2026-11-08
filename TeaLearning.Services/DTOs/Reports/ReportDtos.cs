namespace TeaLearning.Services.DTOs.Reports;

/// <summary>
/// Reporte integral individual de un estudiante generado por sp_GenerarReporteIntegralEstudiante.
/// Alimenta la vista previa gráfica y la exportación PDF en las interfaces de Docente y Acudiente.
/// </summary>
public class StudentComprehensiveReportDto
{
    public Guid StudentProfileId { get; set; }
    public string CodigoEstudiante { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public DateTime FechaNacimiento { get; set; }
    public int EdadAnios { get; set; }
    public string AulaNombre { get; set; } = string.Empty;
    public string AulaGrado { get; set; } = string.Empty;
    public string DocenteNombre { get; set; } = string.Empty;
    public string AcudienteNombre { get; set; } = string.Empty;
    public string ObservacionesMedicasTEA { get; set; } = string.Empty;

    // KPIs Globales
    public int TotalAsignaciones { get; set; }
    public int AsignacionesCompletadas { get; set; }
    public int AsignacionesEnProgreso { get; set; }
    public int AsignacionesPendientes { get; set; }
    public int TotalEjecuciones { get; set; }
    public int EjecucionesExitosas { get; set; }
    public int TotalAciertos { get; set; }
    public int TotalFallos { get; set; }
    public decimal TasaExitoGeneral { get; set; }
    public decimal PrecisionGeneral { get; set; }
    public int TiempoPromedioSegundos { get; set; }
    public decimal? CalificacionPromedio { get; set; }
    public decimal IndiceProgresoPedagogico { get; set; }
    public string TendenciaGeneral { get; set; } = "Estable";
    public int RachaActualDias { get; set; }
    public int MejorRachaDias { get; set; }

    // Perfil Sensorial Activo
    public string PaletaColor { get; set; } = "PastelSuave";
    public string ModoContraste { get; set; } = "Estandar";
    public string Tipografia { get; set; } = "Atkinson Hyperlegible";
    public bool AudioActivo { get; set; } = true;
    public int VolumenAudio { get; set; } = 50;
    public bool TextoAVoz { get; set; } = true;
    public bool AnimacionesReducidas { get; set; } = true;
    public bool OcultarTemporizadores { get; set; } = true;
    public string ModoTemporizador { get; set; } = "sin-temporizador";
    public int NivelEscalaFuente { get; set; } = 2;

    // Síntesis y Metadatos
    public string DiagnosticoPedagogico { get; set; } = string.Empty;
    public string RecomendacionSensorial { get; set; } = string.Empty;
    public DateTime FechaGeneracionUtc { get; set; } = DateTime.UtcNow;
    public int DiasVentana { get; set; } = 30;
    public string GeneradoPorRol { get; set; } = string.Empty;

    // Series para gráficas y tablas
    public List<ReportDailyPointDto> SerieDiaria { get; set; } = [];
    public List<ReportActivityTypeBreakdownDto> DesglosePorTipo { get; set; } = [];
    public List<ReportModuleProgressDto> ProgresoModulos { get; set; } = [];
    public List<ReportRecentAssignmentDto> AsignacionesRecientes { get; set; } = [];
    public List<ReportPedagogicalNoteDto> NotasPedagogicas { get; set; } = [];
}

public class ReportDailyPointDto
{
    public int DiaIndice { get; set; }
    public DateTime Fecha { get; set; }
    public string EtiquetaFecha { get; set; } = string.Empty;
    public int TotalEjecuciones { get; set; }
    public int Completadas { get; set; }
    public int Aciertos { get; set; }
    public int Fallos { get; set; }
    public int TiempoPromedioSeg { get; set; }
    public decimal TasaExitoDia { get; set; }
    public decimal PrecisionDia { get; set; }
    public decimal MediaMovilExito { get; set; }
}

public class ReportActivityTypeBreakdownDto
{
    public int RowNum { get; set; }
    public string TipoActividad { get; set; } = string.Empty;
    public string EtiquetaTipo { get; set; } = string.Empty;
    public int TotalAsignaciones { get; set; }
    public int TotalEjecuciones { get; set; }
    public int Completadas { get; set; }
    public decimal TasaExito { get; set; }
    public decimal PromedioAciertos { get; set; }
    public decimal PromedioFallos { get; set; }
    public int TiempoPromedioSeg { get; set; }
    public decimal? CalificacionPromedio { get; set; }
    public string NivelDominio { get; set; } = "Sin Datos";
}

public class ReportModuleProgressDto
{
    public Guid ModuloId { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public int Orden { get; set; }
    public int ActividadesTotales { get; set; }
    public int ActividadesCompletadas { get; set; }
    public int PorcentajeAvance { get; set; }
    public bool Completado { get; set; }
    public DateTime? FechaCompletado { get; set; }
}

public class ReportRecentAssignmentDto
{
    public Guid AssignmentId { get; set; }
    public string ActividadTitulo { get; set; } = string.Empty;
    public string TipoActividad { get; set; } = string.Empty;
    public string ModuloTitulo { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string Prioridad { get; set; } = string.Empty;
    public decimal? Calificacion { get; set; }
    public string? ComentarioCalificacion { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaCalificacion { get; set; }
}

public class ReportPedagogicalNoteDto
{
    public Guid NoteId { get; set; }
    public DateTime FechaRegistro { get; set; }
    public string Area { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public string? RecomendacionCasa { get; set; }
    public bool VisibleAcudiente { get; set; }
    public string DocenteNombre { get; set; } = string.Empty;
}

/// <summary>
/// Reporte analítico consolidado del aula generado por sp_GenerarReporteConsolidadoAula.
/// </summary>
public class ClassroomConsolidatedReportDto
{
    public Guid ClassroomId { get; set; }
    public string AulaNombre { get; set; } = string.Empty;
    public string Grado { get; set; } = string.Empty;
    public int AnioLectivo { get; set; }
    public string DocenteNombre { get; set; } = string.Empty;
    public string EspecialidadDocente { get; set; } = string.Empty;

    public int TotalEstudiantes { get; set; }
    public int EstudiantesActivosPeriodo { get; set; }
    public int EstudiantesEnAlerta { get; set; }
    public int EstudiantesEnSeguimiento { get; set; }
    public int EstudiantesEstables { get; set; }

    public int TotalAsignacionesAula { get; set; }
    public int AsignacionesCompletadasAula { get; set; }
    public int TotalEjecucionesAula { get; set; }
    public decimal TasaExitoPromedioAula { get; set; }
    public decimal PrecisionPromedioAula { get; set; }
    public int TiempoPromedioSegundosAula { get; set; }
    public decimal? CalificacionPromedioAula { get; set; }
    public string SintesisEjecutivaAula { get; set; } = string.Empty;
    public DateTime FechaGeneracionUtc { get; set; } = DateTime.UtcNow;
    public int DiasVentana { get; set; } = 30;

    public List<ClassroomStudentRiskRowDto> Estudiantes { get; set; } = [];
    public List<ClassroomModuleEffectivenessDto> EfectividadModulos { get; set; } = [];
    public List<ClassroomWeeklyPointDto> EvolucionSemanal { get; set; } = [];
}

public class ClassroomStudentRiskRowDto
{
    public Guid StudentProfileId { get; set; }
    public string CodigoEstudiante { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public int TotalAsignaciones { get; set; }
    public int AsignacionesCompletadas { get; set; }
    public int TotalEjecuciones { get; set; }
    public int EjecucionesExitosas { get; set; }
    public decimal TasaExito { get; set; }
    public decimal PrecisionPromedio { get; set; }
    public int TiempoPromedioSeg { get; set; }
    public decimal? CalificacionPromedio { get; set; }
    public int AvanceModulosPct { get; set; }
    public int AlertasActivas { get; set; }
    public string NivelRiesgo { get; set; } = "Estable";
    public string AccionSugerida { get; set; } = string.Empty;
}

public class ClassroomModuleEffectivenessDto
{
    public Guid ModuloId { get; set; }
    public string ModuloTitulo { get; set; } = string.Empty;
    public string CategoriaNombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    public int TotalAsignaciones { get; set; }
    public int TotalEjecuciones { get; set; }
    public int EjecucionesExitosas { get; set; }
    public decimal TasaExito { get; set; }
    public int TiempoPromedioSeg { get; set; }
    public decimal? CalificacionPromedio { get; set; }
}

public class ClassroomWeeklyPointDto
{
    public int SemanaIndice { get; set; }
    public string EtiquetaSemana { get; set; } = string.Empty;
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public int TotalEjecuciones { get; set; }
    public int EjecucionesExitosas { get; set; }
    public decimal TasaExito { get; set; }
    public decimal PromedioAciertos { get; set; }
    public decimal PromedioFallos { get; set; }
    public int TiempoPromedioSeg { get; set; }
}

public class AdaptiveRoutineRequestDto
{
    public Guid? ClassroomId { get; set; }
    public DateTime? FechaInicio { get; set; }
    public int MaxActividadesPorEstudiante { get; set; } = 2;
}

public class AdaptiveRoutineAssignmentResultDto
{
    public Guid AssignmentId { get; set; }
    public Guid StudentProfileId { get; set; }
    public string CodigoEstudiante { get; set; } = string.Empty;
    public string EstudianteNombre { get; set; } = string.Empty;
    public Guid ActivityId { get; set; }
    public string ActividadTitulo { get; set; } = string.Empty;
    public string TipoActividad { get; set; } = string.Empty;
    public string Prioridad { get; set; } = string.Empty;
    public int OrdenSecuencial { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime FechaLimite { get; set; }
    public string MotivoAdaptativo { get; set; } = string.Empty;
}

public class EvaluateTeaAlertsRequestDto
{
    public Guid? ClassroomId { get; set; }
    public int DiasAnalisis { get; set; } = 14;
    public bool RegistrarNotasAutomaticas { get; set; } = true;
}

public class TeaAlertEvaluationItemDto
{
    public int HallazgoId { get; set; }
    public Guid StudentProfileId { get; set; }
    public string CodigoEstudiante { get; set; } = string.Empty;
    public string EstudianteNombre { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string TipoAlerta { get; set; } = string.Empty;
    public string Severidad { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string DescripcionHallazgo { get; set; } = string.Empty;
    public string RecomendacionCasa { get; set; } = string.Empty;
    public string AjusteSensorialSugerido { get; set; } = string.Empty;
    public bool NotaInsertada { get; set; }
}

public class RecalculateStudentProgressDto
{
    public Guid StudentProfileId { get; set; }
    public int AsignacionesReconciliadas { get; set; }
    public int TotalActividadesCompletadas { get; set; }
    public int ModulosCompletados { get; set; }
    public int InsigniasDesbloqueadas { get; set; }
    public string MensajeMotivacional { get; set; } = string.Empty;
    public DateTime FechaRecalculoUtc { get; set; } = DateTime.UtcNow;
    public List<RecalculatedBadgeDto> Insignias { get; set; } = [];
    public List<ReportModuleProgressDto> Modulos { get; set; } = [];
}

public class RecalculatedBadgeDto
{
    public string Clave { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Icono { get; set; } = string.Empty;
    public bool Desbloqueado { get; set; }
    public DateTime? FechaDesbloqueo { get; set; }
}
