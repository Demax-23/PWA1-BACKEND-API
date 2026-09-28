using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Achievements;

namespace TeaLearning.Services.Interfaces;

public interface IAchievementService
{
    Task<ApiResponse<StudentAchievementsDto>> GetStudentAchievementsByUserIdAsync(
        Guid userId, 
        CancellationToken cancellationToken = default);

    Task<ApiResponse<StudentAchievementsDto>> GetStudentAchievementsByIdAsync(
        Guid studentId, 
        CancellationToken cancellationToken = default);
}
