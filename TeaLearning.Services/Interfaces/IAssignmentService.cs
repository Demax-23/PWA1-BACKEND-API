using TeaLearning.Core.Enums;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Assignments;

namespace TeaLearning.Services.Interfaces;

public interface IAssignmentService
{
    Task<ApiResponse<List<AssignmentDto>>> CreateAssignmentAsync(
        CreateAssignmentDto dto, 
        CancellationToken cancellationToken = default);

    Task<ApiResponse<List<AssignmentDto>>> GetAssignmentsAsync(
        Guid? studentProfileId = null, 
        Guid? classroomId = null, 
        EstadoAsignacion? estado = null, 
        Guid? currentUserId = null,
        bool isGuardianOnly = false,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<AssignmentDto>> GetAssignmentByIdAsync(
        Guid id, 
        Guid? currentUserId = null,
        bool isStaff = true,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> DeleteAssignmentAsync(
        Guid id, 
        Guid? currentUserId = null,
        bool isAdmin = true,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<AssignmentGradeDto>> GradeAssignmentAsync(
        Guid assignmentId, 
        Guid currentUserId, 
        GradeAssignmentDto dto, 
        bool isAdmin = false,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> RemoveGradeAsync(
        Guid assignmentId, 
        Guid currentUserId, 
        bool isAdmin = false,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<List<AssignmentGradeDto>>> GetStudentGradesAsync(
        Guid studentProfileId, 
        Guid currentUserId, 
        CancellationToken cancellationToken = default);
}
