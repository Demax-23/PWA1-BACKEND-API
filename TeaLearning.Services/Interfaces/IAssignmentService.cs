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
        CancellationToken cancellationToken = default);

    Task<ApiResponse<AssignmentDto>> GetAssignmentByIdAsync(
        Guid id, 
        CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> DeleteAssignmentAsync(
        Guid id, 
        CancellationToken cancellationToken = default);
}
