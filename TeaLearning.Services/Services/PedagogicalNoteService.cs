using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Data.Context;
using TeaLearning.Data.Repositories;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Pedagogical;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Services.Services;

public class PedagogicalNoteService : IPedagogicalNoteService
{
    private readonly IPedagogicalNoteRepository _noteRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly ApplicationDbContext _context;

    public PedagogicalNoteService(
        IPedagogicalNoteRepository noteRepository,
        IStudentRepository studentRepository,
        ApplicationDbContext context)
    {
        _noteRepository = noteRepository;
        _studentRepository = studentRepository;
        _context = context;
    }

    public async Task<ApiResponse<PedagogicalNoteDto>> CreateNoteAsync(
        Guid studentId, 
        Guid currentUserId, 
        CreatePedagogicalNoteDto dto, 
        CancellationToken cancellationToken = default)
    {
        var student = await _studentRepository.GetStudentWithDetailsAsync(studentId, cancellationToken);
        if (student == null)
        {
            return ApiResponse<PedagogicalNoteDto>.Fail($"Estudiante con ID {studentId} no encontrado.");
        }

        // Buscar perfil docente correspondiente al usuario autenticado
        var teacherProfile = await _context.TeacherProfiles
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.UserId == currentUserId, cancellationToken);

        if (teacherProfile == null)
        {
            // Si el usuario es administrador sin perfil docente asignado, creamos el perfil correspondiente
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == currentUserId, cancellationToken);
            if (user == null)
            {
                return ApiResponse<PedagogicalNoteDto>.Fail("Usuario docente o administrador no encontrado.");
            }

            teacherProfile = new TeacherProfile
            {
                UserId = currentUserId,
                Especialidad = "Acompañamiento y Supervisión Pedagógica TEA",
                NumeroRegistroProfesional = "REG-PED-" + DateTime.UtcNow.Ticks.ToString()[^6..]
            };
            _context.TeacherProfiles.Add(teacherProfile);
            await _context.SaveChangesAsync(cancellationToken);

            teacherProfile.User = user;
        }

        var note = new PedagogicalNote
        {
            StudentProfileId = studentId,
            TeacherProfileId = teacherProfile.Id,
            Contenido = dto.Contenido.Trim(),
            FechaRegistro = DateTime.UtcNow,
            IsActive = true
        };

        await _noteRepository.AddAsync(note, cancellationToken);

        var resultDto = new PedagogicalNoteDto
        {
            Id = note.Id,
            StudentProfileId = note.StudentProfileId,
            StudentNombre = student.User?.NombreCompleto ?? string.Empty,
            TeacherProfileId = note.TeacherProfileId,
            TeacherNombre = teacherProfile.User?.NombreCompleto ?? string.Empty,
            Contenido = note.Contenido,
            FechaRegistro = note.FechaRegistro
        };

        return ApiResponse<PedagogicalNoteDto>.Ok(resultDto, "Nota pedagógica registrada exitosamente.");
    }

    public async Task<ApiResponse<IReadOnlyList<PedagogicalNoteDto>>> GetNotesByStudentIdAsync(
        Guid studentId, 
        Guid currentUserId, 
        bool isStaff, 
        CancellationToken cancellationToken = default)
    {
        var student = await _studentRepository.GetStudentWithDetailsAsync(studentId, cancellationToken);
        if (student == null)
        {
            return ApiResponse<IReadOnlyList<PedagogicalNoteDto>>.Fail($"Estudiante con ID {studentId} no encontrado.");
        }

        // Si no es parte del equipo docente/admin, validar que sea el acudiente registrado del estudiante
        if (!isStaff)
        {
            if (student.GuardianProfile == null || student.GuardianProfile.UserId != currentUserId)
            {
                return ApiResponse<IReadOnlyList<PedagogicalNoteDto>>.Fail("No tiene autorización para visualizar las notas pedagógicas de este estudiante.");
            }
        }

        var notes = await _noteRepository.GetByStudentIdAsync(studentId, cancellationToken);

        var dtoList = notes.Select(n => new PedagogicalNoteDto
        {
            Id = n.Id,
            StudentProfileId = n.StudentProfileId,
            StudentNombre = student.User?.NombreCompleto ?? string.Empty,
            TeacherProfileId = n.TeacherProfileId,
            TeacherNombre = n.TeacherProfile?.User?.NombreCompleto ?? "Docente",
            Contenido = n.Contenido,
            FechaRegistro = n.FechaRegistro
        }).ToList();

        return ApiResponse<IReadOnlyList<PedagogicalNoteDto>>.Ok(dtoList, "Notas pedagógicas recuperadas exitosamente.");
    }
}
