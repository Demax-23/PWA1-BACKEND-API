using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Data.Repositories;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Execution;
using TeaLearning.Services.DTOs.Sensory;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Services.Services;

public class ActivityExecutionService : IActivityExecutionService
{
    private readonly IActivityAssignmentRepository _assignmentRepository;
    private readonly IActivityExecutionRepository _executionRepository;
    private readonly IStudentRepository _studentRepository;

    public ActivityExecutionService(
        IActivityAssignmentRepository assignmentRepository,
        IActivityExecutionRepository executionRepository,
        IStudentRepository studentRepository)
    {
        _assignmentRepository = assignmentRepository;
        _executionRepository = executionRepository;
        _studentRepository = studentRepository;
    }

    public async Task<ApiResponse<StartActivityResponseDto>> StartActivityAsync(
        Guid assignmentId, 
        Guid studentUserId, 
        CancellationToken cancellationToken = default)
    {
        // 1. Obtener la asignación con detalles (Actividad, Perfil de Estudiante y Preferencias Sensoriales)
        var assignment = await _assignmentRepository.GetWithDetailsAsync(assignmentId, cancellationToken);
        if (assignment == null)
        {
            return ApiResponse<StartActivityResponseDto>.Fail($"Asignación con ID {assignmentId} no encontrada.");
        }

        // 2. Validar que la asignación pertenezca al estudiante autenticado
        if (assignment.StudentProfile.UserId != studentUserId)
        {
            return ApiResponse<StartActivityResponseDto>.Fail("No tiene autorización para iniciar una actividad asignada a otro estudiante.");
        }

        // 3. Transicionar estado a EnProgreso si estaba en Pendiente
        if (assignment.Estado == EstadoAsignacion.Pendiente)
        {
            assignment.Estado = EstadoAsignacion.EnProgreso;
            await _assignmentRepository.UpdateAsync(assignment, cancellationToken);
        }

        // 4. Mapear preferencias sensoriales para adaptación inmediata de la interfaz
        SensoryPreferenceDto? sensoryDto = null;
        var sensoryPref = assignment.StudentProfile.SensoryPreference;
        if (sensoryPref != null)
        {
            sensoryDto = new SensoryPreferenceDto
            {
                Id = sensoryPref.Id,
                StudentProfileId = sensoryPref.StudentProfileId,
                PaletaColor = sensoryPref.PaletaColor,
                Tipografia = sensoryPref.Tipografia,
                AudioActivo = sensoryPref.AudioActivo,
                VolumenAudio = sensoryPref.VolumenAudio,
                TextoAVoz = sensoryPref.TextoAVoz,
                MayorEspacioEntreLetras = sensoryPref.MayorEspacioEntreLetras,
                AnimacionesReducidas = sensoryPref.AnimacionesReducidas,
                NivelEscalaFuente = sensoryPref.NivelEscalaFuente,
                ModoContraste = sensoryPref.ModoContraste,
                OcultarTemporizadores = sensoryPref.OcultarTemporizadores
            };
        }

        var responseDto = new StartActivityResponseDto
        {
            AssignmentId = assignment.Id,
            ActivityId = assignment.ActivityId,
            Titulo = assignment.Activity.Titulo,
            TipoActividad = assignment.Activity.TipoActividad.ToString(),
            ObjetivoPedagogico = assignment.Activity.ObjetivoPedagogico,
            ConfigJson = assignment.Activity.ConfigJson,
            Estado = assignment.Estado.ToString(),
            FechaInicio = assignment.FechaInicio,
            SensoryAdaptations = sensoryDto
        };

        return ApiResponse<StartActivityResponseDto>.Ok(
            responseDto, 
            "Actividad iniciada exitosamente con adaptaciones sensoriales personalizadas");
    }

    public async Task<ApiResponse<ExecutionResultDto>> SubmitActivityAsync(
        Guid assignmentId, 
        Guid studentUserId, 
        SubmitActivityDto dto, 
        CancellationToken cancellationToken = default)
    {
        // 1. Obtener la asignación con detalles
        var assignment = await _assignmentRepository.GetWithDetailsAsync(assignmentId, cancellationToken);
        if (assignment == null)
        {
            return ApiResponse<ExecutionResultDto>.Fail($"Asignación con ID {assignmentId} no encontrada.");
        }

        // 2. Validar pertenencia del estudiante
        if (assignment.StudentProfile.UserId != studentUserId)
        {
            return ApiResponse<ExecutionResultDto>.Fail("No tiene autorización para enviar resultados de una asignación que no le corresponde.");
        }

        // 3. Registrar telemetría del intento / ejecución en ActivityExecution
        var execution = new ActivityExecution
        {
            AssignmentId = assignment.Id,
            StudentProfileId = assignment.StudentProfileId,
            TiempoRespuestaSegundos = dto.TiempoRespuestaSegundos,
            CantidadIntentosFallidos = dto.CantidadIntentosFallidos,
            CantidadAciertos = dto.CantidadAciertos,
            CompletadaExitosamente = dto.CompletadaExitosamente,
            FechaRegistro = DateTime.UtcNow,
            IsActive = true
        };

        await _executionRepository.AddAsync(execution, cancellationToken);

        // 4. Si fue completada exitosamente, actualizar estado de la asignación
        if (dto.CompletadaExitosamente)
        {
            assignment.Estado = EstadoAsignacion.Completada;
            await _assignmentRepository.UpdateAsync(assignment, cancellationToken);
        }

        var resultDto = new ExecutionResultDto
        {
            ExecutionId = execution.Id,
            AssignmentId = assignment.Id,
            StudentProfileId = assignment.StudentProfileId,
            TiempoRespuestaSegundos = execution.TiempoRespuestaSegundos,
            CantidadIntentosFallidos = execution.CantidadIntentosFallidos,
            CantidadAciertos = execution.CantidadAciertos,
            CompletadaExitosamente = execution.CompletadaExitosamente,
            FechaRegistro = execution.FechaRegistro,
            NuevoEstadoAsignacion = assignment.Estado.ToString()
        };

        string feedbackMsg = dto.CompletadaExitosamente 
            ? "¡Excelente trabajo! Has completado la actividad con éxito." 
            : "Intento registrado correctamente. Puedes continuar practicando.";

        return ApiResponse<ExecutionResultDto>.Ok(resultDto, feedbackMsg);
    }
}
