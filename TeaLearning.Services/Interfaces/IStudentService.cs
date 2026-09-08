using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Students;

namespace TeaLearning.Services.Interfaces;

public interface IStudentService
{
    Task<ApiResponse<List<DailyRoutineItemDto>>> GetDailyRoutineForStudentUserAsync(
        Guid userId, 
        DateTime? targetDate = null, 
        CancellationToken cancellationToken = default);

    Task<ApiResponse<StudentProfileDto>> GetStudentProfileByUserIdAsync(
        Guid userId, 
        CancellationToken cancellationToken = default);
}
