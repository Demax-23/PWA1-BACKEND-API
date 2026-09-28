using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Guardian;

namespace TeaLearning.Services.Interfaces;

public interface IGuardianMonitoringService
{
    Task<ApiResponse<GuardianStudentProgressDto>> GetStudentProgressAsync(
        Guid studentId, 
        Guid currentUserId, 
        bool isAdmin, 
        CancellationToken cancellationToken = default);

    Task<ApiResponse<ActivityHistoryPagedDto>> GetActivityHistoryAsync(
        Guid studentId,
        Guid currentUserId,
        bool isAdmin,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}
