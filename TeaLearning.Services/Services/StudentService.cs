using TeaLearning.Data.Repositories;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Students;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Services.Services;

public class StudentService : IStudentService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IActivityAssignmentRepository _assignmentRepository;

    public StudentService(
        IStudentRepository studentRepository, 
        IActivityAssignmentRepository assignmentRepository)
    {
        _studentRepository = studentRepository;
        _assignmentRepository = assignmentRepository;
    }

    public async Task<ApiResponse<List<DailyRoutineItemDto>>> GetDailyRoutineForStudentUserAsync(
        Guid userId, 
        DateTime? targetDate = null, 
        CancellationToken cancellationToken = default)
    {
        var studentProfile = await _studentRepository.GetByUserIdAsync(userId, cancellationToken);
        if (studentProfile == null)
        {
            return ApiResponse<List<DailyRoutineItemDto>>.Fail("No se encontró el perfil de estudiante para este usuario.");
        }

        var queryDate = targetDate ?? DateTime.UtcNow;
        var assignments = await _assignmentRepository.GetDailyRoutineByStudentIdAsync(studentProfile.Id, queryDate, cancellationToken);

        var routineItems = assignments
            .OrderBy(a => a.OrdenSecuencial)
            .Select(a => new DailyRoutineItemDto
            {
                AssignmentId = a.Id,
                ActivityId = a.ActivityId,
                Titulo = a.Activity.Titulo,
                Descripcion = a.Activity.Descripcion,
                TipoActividad = a.Activity.TipoActividad.ToString(),
                ObjetivoPedagogico = a.Activity.ObjetivoPedagogico,
                ConfigJson = a.Activity.ConfigJson,
                ModuloTitulo = a.Activity.Modulo?.Titulo ?? string.Empty,
                OrdenSecuencial = a.OrdenSecuencial,
                Prioridad = a.Prioridad.ToString(),
                Estado = a.Estado.ToString(),
                FechaInicio = a.FechaInicio,
                FechaLimite = a.FechaLimite
            })
            .ToList();

        return ApiResponse<List<DailyRoutineItemDto>>.Ok(
            routineItems, 
            $"Rutina pedagógica del día recuperada exitosamente ({routineItems.Count} actividades programadas)");
    }

    public async Task<ApiResponse<StudentProfileDto>> GetStudentProfileByUserIdAsync(
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        var studentProfile = await _studentRepository.GetByUserIdAsync(userId, cancellationToken);
        if (studentProfile == null)
        {
            return ApiResponse<StudentProfileDto>.Fail("Perfil de estudiante no encontrado.");
        }

        var dto = new StudentProfileDto
        {
            Id = studentProfile.Id,
            UserId = studentProfile.UserId,
            NombreCompleto = studentProfile.User?.NombreCompleto ?? string.Empty,
            CodigoEstudiante = studentProfile.CodigoEstudiante,
            FechaNacimiento = studentProfile.FechaNacimiento,
            ObservacionesMedicasTEA = studentProfile.ObservacionesMedicasTEA,
            ClassroomId = studentProfile.ClassroomId,
            ClassroomNombre = studentProfile.Classroom?.Nombre,
            GuardianProfileId = studentProfile.GuardianProfileId,
            GuardianNombre = studentProfile.GuardianProfile?.User?.NombreCompleto
        };

        return ApiResponse<StudentProfileDto>.Ok(dto, "Perfil de estudiante recuperado exitosamente");
    }

    public async Task<ApiResponse<List<StudentProfileDto>>> GetAllStudentsAsync(CancellationToken cancellationToken = default)
    {
        var students = await _studentRepository.GetAllWithDetailsAsync(cancellationToken);
        var dtos = students.Select(studentProfile => new StudentProfileDto
        {
            Id = studentProfile.Id,
            UserId = studentProfile.UserId,
            NombreCompleto = studentProfile.User?.NombreCompleto ?? string.Empty,
            CodigoEstudiante = studentProfile.CodigoEstudiante,
            FechaNacimiento = studentProfile.FechaNacimiento,
            ObservacionesMedicasTEA = studentProfile.ObservacionesMedicasTEA,
            ClassroomId = studentProfile.ClassroomId,
            ClassroomNombre = studentProfile.Classroom?.Nombre,
            GuardianProfileId = studentProfile.GuardianProfileId,
            GuardianNombre = studentProfile.GuardianProfile?.User?.NombreCompleto
        }).ToList();

        return ApiResponse<List<StudentProfileDto>>.Ok(dtos, $"Se recuperaron {dtos.Count} estudiantes");
    }

    public async Task<ApiResponse<List<StudentProfileDto>>> GetStudentsForGuardianUserAsync(
        Guid guardianUserId,
        CancellationToken cancellationToken = default)
    {
        var allStudents = await _studentRepository.GetAllWithDetailsAsync(cancellationToken);
        var guardianStudents = allStudents
            .Where(s => s.GuardianProfile != null && s.GuardianProfile.UserId == guardianUserId)
            .Select(studentProfile => new StudentProfileDto
            {
                Id = studentProfile.Id,
                UserId = studentProfile.UserId,
                NombreCompleto = studentProfile.User?.NombreCompleto ?? string.Empty,
                CodigoEstudiante = studentProfile.CodigoEstudiante,
                FechaNacimiento = studentProfile.FechaNacimiento,
                ObservacionesMedicasTEA = studentProfile.ObservacionesMedicasTEA,
                ClassroomId = studentProfile.ClassroomId,
                ClassroomNombre = studentProfile.Classroom?.Nombre,
                GuardianProfileId = studentProfile.GuardianProfileId,
                GuardianNombre = studentProfile.GuardianProfile?.User?.NombreCompleto
            }).ToList();

        return ApiResponse<List<StudentProfileDto>>.Ok(guardianStudents, $"Se recuperaron {guardianStudents.Count} estudiantes para el acudiente");
    }
}
