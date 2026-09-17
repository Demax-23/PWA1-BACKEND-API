using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Execution;

namespace TeaLearning.Services.Interfaces;

public interface IActivityExecutionService
{
    Task<ApiResponse<StartActivityResponseDto>> StartActivityAsync(
        Guid assignmentId, 
        Guid studentUserId, 
        CancellationToken cancellationToken = default);

    Task<ApiResponse<ExecutionResultDto>> SubmitActivityAsync(
        Guid assignmentId, 
        Guid studentUserId, 
        SubmitActivityDto dto, 
        CancellationToken cancellationToken = default);
}
