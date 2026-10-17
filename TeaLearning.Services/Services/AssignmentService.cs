using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Data.Context;
using TeaLearning.Data.Repositories;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Assignments;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Services.Services;

public class AssignmentService : IAssignmentService
{
    private readonly IActivityAssignmentRepository _assignmentRepository;
    private readonly IActivityRepository _activityRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly ApplicationDbContext _context;

    public AssignmentService(
        IActivityAssignmentRepository assignmentRepository,
        IActivityRepository activityRepository,
        IStudentRepository studentRepository,
        ApplicationDbContext context)
    {
        _assignmentRepository = assignmentRepository;
        _activityRepository = activityRepository;
        _studentRepository = studentRepository;
        _context = context;
    }

    public async Task<ApiResponse<List<AssignmentDto>>> CreateAssignmentAsync(
        CreateAssignmentDto dto, 
        CancellationToken cancellationToken = default)
    {
        // 1. Validar que la actividad exista y esté activa
        var activity = await _activityRepository.GetByIdAsync(dto.ActivityId, cancellationToken);
        if (activity == null)
        {
            return ApiResponse<List<AssignmentDto>>.Fail($"La actividad pedagógica con ID {dto.ActivityId} no existe.");
        }

        var createdAssignments = new List<ActivityAssignment>();

        // 2. Asignación Individual
        if (dto.StudentProfileId.HasValue)
        {
            var student = await _studentRepository.GetByIdAsync(dto.StudentProfileId.Value, cancellationToken);
            if (student == null)
            {
                return ApiResponse<List<AssignmentDto>>.Fail($"El estudiante con ID {dto.StudentProfileId.Value} no existe.");
            }

            var existingAssignment = await _context.ActivityAssignments
                .FirstOrDefaultAsync(aa => aa.ActivityId == dto.ActivityId 
                                        && aa.StudentProfileId == dto.StudentProfileId.Value 
                                        && aa.IsActive, cancellationToken);

            if (existingAssignment != null)
            {
                existingAssignment.FechaInicio = dto.FechaInicio;
                if (dto.FechaLimite.HasValue) existingAssignment.FechaLimite = dto.FechaLimite;
                existingAssignment.OrdenSecuencial = dto.OrdenSecuencial;
                existingAssignment.Prioridad = dto.Prioridad;
                await _context.SaveChangesAsync(cancellationToken);
                createdAssignments.Add(existingAssignment);
            }
            else
            {
                var assignment = new ActivityAssignment
                {
                    ActivityId = dto.ActivityId,
                    StudentProfileId = dto.StudentProfileId.Value,
                    FechaInicio = dto.FechaInicio,
                    FechaLimite = dto.FechaLimite,
                    OrdenSecuencial = dto.OrdenSecuencial,
                    Prioridad = dto.Prioridad,
                    Estado = EstadoAsignacion.Pendiente,
                    IsActive = true
                };

                await _assignmentRepository.AddAsync(assignment, cancellationToken);
                createdAssignments.Add(assignment);
            }
        }
        // 3. Asignación Masiva por Aula
        else if (dto.ClassroomId.HasValue)
        {
            var classroom = await _context.Classrooms
                .Include(c => c.Students.Where(s => s.IsActive))
                .FirstOrDefaultAsync(c => c.Id == dto.ClassroomId.Value && c.IsActive, cancellationToken);

            if (classroom == null)
            {
                return ApiResponse<List<AssignmentDto>>.Fail($"El aula con ID {dto.ClassroomId.Value} no existe.");
            }

            var students = classroom.Students.ToList();
            if (!students.Any())
            {
                return ApiResponse<List<AssignmentDto>>.Fail($"El aula '{classroom.Nombre}' no cuenta con estudiantes activos para asignar actividades.");
            }

            var assignmentsToAdd = new List<ActivityAssignment>();
            foreach (var student in students)
            {
                var existingAssignment = await _context.ActivityAssignments
                    .FirstOrDefaultAsync(aa => aa.ActivityId == dto.ActivityId 
                                            && aa.StudentProfileId == student.Id 
                                            && aa.IsActive, cancellationToken);
                if (existingAssignment != null)
                {
                    existingAssignment.FechaInicio = dto.FechaInicio;
                    if (dto.FechaLimite.HasValue) existingAssignment.FechaLimite = dto.FechaLimite;
                    existingAssignment.OrdenSecuencial = dto.OrdenSecuencial;
                    existingAssignment.Prioridad = dto.Prioridad;
                    createdAssignments.Add(existingAssignment);
                }
                else
                {
                    var newAssignment = new ActivityAssignment
                    {
                        ActivityId = dto.ActivityId,
                        StudentProfileId = student.Id,
                        FechaInicio = dto.FechaInicio,
                        FechaLimite = dto.FechaLimite,
                        OrdenSecuencial = dto.OrdenSecuencial,
                        Prioridad = dto.Prioridad,
                        Estado = EstadoAsignacion.Pendiente,
                        IsActive = true
                    };
                    assignmentsToAdd.Add(newAssignment);
                    createdAssignments.Add(newAssignment);
                }
            }

            if (assignmentsToAdd.Any())
            {
                await _assignmentRepository.AddRangeAsync(assignmentsToAdd, cancellationToken);
            }
            else
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            return ApiResponse<List<AssignmentDto>>.Fail("Debe especificar un StudentProfileId para asignación individual o un ClassroomId para asignación por aula.");
        }

        // Recuperar con detalles para armar el DTO de respuesta
        var dtoList = new List<AssignmentDto>();
        foreach (var a in createdAssignments)
        {
            var detailed = await _assignmentRepository.GetWithDetailsAsync(a.Id, cancellationToken);
            if (detailed != null)
            {
                dtoList.Add(MapToDto(detailed));
            }
        }

        return ApiResponse<List<AssignmentDto>>.Ok(
            dtoList, 
            $"Se crearon exitosamente {dtoList.Count} asignaciones de actividad");
    }

    public async Task<ApiResponse<List<AssignmentDto>>> GetAssignmentsAsync(
        Guid? studentProfileId = null, 
        Guid? classroomId = null, 
        EstadoAsignacion? estado = null, 
        Guid? currentUserId = null,
        bool isGuardianOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (isGuardianOnly && currentUserId.HasValue)
        {
            if (studentProfileId.HasValue)
            {
                var student = await _studentRepository.GetStudentWithDetailsAsync(studentProfileId.Value, cancellationToken);
                if (student == null)
                {
                    return ApiResponse<List<AssignmentDto>>.Fail($"El estudiante con ID {studentProfileId.Value} no existe.");
                }
                if (student.GuardianProfile == null || student.GuardianProfile.UserId != currentUserId.Value)
                {
                    return ApiResponse<List<AssignmentDto>>.Fail("No tiene autorización para consultar las asignaciones de este estudiante.");
                }
            }

            var allFiltered = await _assignmentRepository.GetFilteredAsync(studentProfileId, classroomId, estado, cancellationToken);
            var guardianFiltered = allFiltered
                .Where(a => a.StudentProfile?.GuardianProfile != null && a.StudentProfile.GuardianProfile.UserId == currentUserId.Value)
                .Select(MapToDto)
                .ToList();
            return ApiResponse<List<AssignmentDto>>.Ok(guardianFiltered, $"Se recuperaron {guardianFiltered.Count} asignaciones");
        }

        var assignments = await _assignmentRepository.GetFilteredAsync(studentProfileId, classroomId, estado, cancellationToken);
        var dtoList = assignments.Select(MapToDto).ToList();
        return ApiResponse<List<AssignmentDto>>.Ok(dtoList, $"Se recuperaron {dtoList.Count} asignaciones");
    }

    public async Task<ApiResponse<AssignmentDto>> GetAssignmentByIdAsync(
        Guid id, 
        Guid? currentUserId = null,
        bool isStaff = true,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _assignmentRepository.GetWithDetailsAsync(id, cancellationToken);
        if (assignment == null)
        {
            return ApiResponse<AssignmentDto>.Fail($"Asignación con ID {id} no encontrada.");
        }

        if (!isStaff && currentUserId.HasValue)
        {
            bool isOwnStudent = assignment.StudentProfile?.UserId == currentUserId.Value;
            bool isLinkedGuardian = assignment.StudentProfile?.GuardianProfile != null &&
                                    assignment.StudentProfile.GuardianProfile.UserId == currentUserId.Value;
            if (!isOwnStudent && !isLinkedGuardian)
            {
                return ApiResponse<AssignmentDto>.Fail("No tiene autorización para consultar esta asignación.");
            }
        }

        return ApiResponse<AssignmentDto>.Ok(MapToDto(assignment), "Asignación recuperada con éxito");
    }

    public async Task<ApiResponse<bool>> DeleteAssignmentAsync(
        Guid id, 
        Guid? currentUserId = null,
        bool isAdmin = true,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _assignmentRepository.GetWithDetailsAsync(id, cancellationToken)
                         ?? await _assignmentRepository.GetByIdAsync(id, cancellationToken);
        if (assignment == null)
        {
            return ApiResponse<bool>.Fail($"Asignación con ID {id} no encontrada.");
        }

        if (!isAdmin && currentUserId.HasValue)
        {
            var teacher = await _context.TeacherProfiles
                .FirstOrDefaultAsync(t => t.UserId == currentUserId.Value && t.IsActive, cancellationToken);

            if (teacher != null && assignment.Activity != null && assignment.Activity.DocenteId != Guid.Empty)
            {
                bool isActivityOwner = assignment.Activity.DocenteId == teacher.Id;
                bool isClassroomOwner = assignment.StudentProfile?.Classroom != null &&
                                        assignment.StudentProfile.Classroom.TeacherProfileId == teacher.Id;
                if (!isActivityOwner && !isClassroomOwner)
                {
                    return ApiResponse<bool>.Fail("No tiene autorización para eliminar una asignación perteneciente a otro docente.");
                }
            }
        }

        await _assignmentRepository.DeleteAsync(assignment, cancellationToken);
        return ApiResponse<bool>.Ok(true, "Asignación eliminada exitosamente");
    }

    public async Task<ApiResponse<AssignmentGradeDto>> GradeAssignmentAsync(
        Guid assignmentId, 
        Guid currentUserId, 
        GradeAssignmentDto dto, 
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _assignmentRepository.GetWithDetailsAsync(assignmentId, cancellationToken);
        if (assignment == null)
        {
            return ApiResponse<AssignmentGradeDto>.Fail($"La asignación con ID {assignmentId} no existe.");
        }

        var teacher = await _context.TeacherProfiles
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.UserId == currentUserId && t.IsActive, cancellationToken);

        if (!isAdmin && teacher != null && assignment.Activity != null && assignment.Activity.DocenteId != Guid.Empty)
        {
            bool isActivityOwner = assignment.Activity.DocenteId == teacher.Id;
            bool isClassroomOwner = assignment.StudentProfile?.Classroom != null &&
                                    assignment.StudentProfile.Classroom.TeacherProfileId == teacher.Id;
            if (!isActivityOwner && !isClassroomOwner)
            {
                return ApiResponse<AssignmentGradeDto>.Fail("No tiene autorización para calificar una asignación perteneciente a otro docente.");
            }
        }

        var currentUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == currentUserId, cancellationToken);

        assignment.Calificacion = Math.Round(dto.Calificacion, 1);
        assignment.ComentarioCalificacion = string.IsNullOrWhiteSpace(dto.Comentario) ? null : dto.Comentario.Trim();
        assignment.FechaCalificacion = DateTime.UtcNow;

        if (teacher != null)
        {
            assignment.CalificadoPorDocenteId = teacher.Id;
            assignment.CalificadoPorDocente = teacher;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var responseDto = MapToGradeDto(assignment, teacher?.User?.NombreCompleto ?? currentUser?.NombreCompleto);
        return ApiResponse<AssignmentGradeDto>.Ok(responseDto, "Calificación guardada exitosamente.");
    }

    public async Task<ApiResponse<bool>> RemoveGradeAsync(
        Guid assignmentId, 
        Guid currentUserId, 
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _assignmentRepository.GetWithDetailsAsync(assignmentId, cancellationToken)
                         ?? await _assignmentRepository.GetByIdAsync(assignmentId, cancellationToken);
        if (assignment == null)
        {
            return ApiResponse<bool>.Fail($"La asignación con ID {assignmentId} no existe.");
        }

        if (!isAdmin)
        {
            var teacher = await _context.TeacherProfiles
                .FirstOrDefaultAsync(t => t.UserId == currentUserId && t.IsActive, cancellationToken);

            if (teacher != null)
            {
                bool isActivityOwner = assignment.Activity == null || assignment.Activity.DocenteId == Guid.Empty || assignment.Activity.DocenteId == teacher.Id;
                bool isGraderOwner = !assignment.CalificadoPorDocenteId.HasValue || assignment.CalificadoPorDocenteId.Value == teacher.Id;
                bool isClassroomOwner = assignment.StudentProfile?.Classroom != null &&
                                        assignment.StudentProfile.Classroom.TeacherProfileId == teacher.Id;

                if (!isActivityOwner && !isGraderOwner && !isClassroomOwner)
                {
                    return ApiResponse<bool>.Fail("No tiene autorización para eliminar la calificación registrada por otro docente.");
                }
            }
        }

        assignment.Calificacion = null;
        assignment.ComentarioCalificacion = null;
        assignment.FechaCalificacion = null;
        assignment.CalificadoPorDocenteId = null;

        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Ok(true, "Calificación removida exitosamente.");
    }

    public async Task<ApiResponse<List<AssignmentGradeDto>>> GetStudentGradesAsync(
        Guid studentProfileId, 
        Guid currentUserId, 
        CancellationToken cancellationToken = default)
    {
        var student = await _studentRepository.GetByIdAsync(studentProfileId, cancellationToken);
        if (student == null)
        {
            return ApiResponse<List<AssignmentGradeDto>>.Fail($"El estudiante con ID {studentProfileId} no existe.");
        }

        var assignments = await _assignmentRepository.GetFilteredAsync(
            studentProfileId: studentProfileId, 
            cancellationToken: cancellationToken);

        var list = assignments.Select(a => MapToGradeDto(a)).ToList();
        return ApiResponse<List<AssignmentGradeDto>>.Ok(list, "Calificaciones recuperadas exitosamente.");
    }

    private static AssignmentGradeDto MapToGradeDto(ActivityAssignment a, string? fallbackDocenteNombre = null)
    {
        var lastAttempt = a.Executions?
            .OrderByDescending(e => e.FechaRegistro)
            .FirstOrDefault();

        return new AssignmentGradeDto
        {
            AssignmentId = a.Id,
            ActivityId = a.ActivityId,
            Titulo = a.Activity?.Titulo ?? "Actividad pedagógica",
            TipoActividad = a.Activity?.TipoActividad.ToString() ?? "Actividad",
            Calificacion = a.Calificacion,
            Comentario = a.ComentarioCalificacion,
            FechaCalificacion = a.FechaCalificacion,
            DocenteNombre = a.CalificadoPorDocente?.User?.NombreCompleto ?? fallbackDocenteNombre,
            PuedeEditar = true,
            UltimoIntento = lastAttempt == null ? null : new ActivityLastAttemptDto
            {
                FechaRegistro = lastAttempt.FechaRegistro,
                CantidadAciertos = lastAttempt.CantidadAciertos,
                CantidadIntentosFallidos = lastAttempt.CantidadIntentosFallidos,
                CompletadaExitosamente = lastAttempt.CompletadaExitosamente,
                TiempoRespuestaSegundos = lastAttempt.TiempoRespuestaSegundos
            },
            TotalIntentos = a.Executions?.Count ?? 0
        };
    }

    private static AssignmentDto MapToDto(ActivityAssignment a)
    {
        return new AssignmentDto
        {
            Id = a.Id,
            ActivityId = a.ActivityId,
            ActivityTitulo = a.Activity?.Titulo ?? string.Empty,
            TipoActividad = a.Activity?.TipoActividad.ToString() ?? string.Empty,
            StudentProfileId = a.StudentProfileId,
            StudentNombre = a.StudentProfile?.User?.NombreCompleto ?? string.Empty,
            CodigoEstudiante = a.StudentProfile?.CodigoEstudiante ?? string.Empty,
            FechaInicio = a.FechaInicio,
            FechaLimite = a.FechaLimite,
            OrdenSecuencial = a.OrdenSecuencial,
            Prioridad = a.Prioridad,
            Estado = a.Estado,
            CreatedAt = a.CreatedAt
        };
    }
}
