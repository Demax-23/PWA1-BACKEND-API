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
}
