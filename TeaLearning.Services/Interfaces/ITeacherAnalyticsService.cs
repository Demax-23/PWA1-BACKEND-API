using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Analytics;

namespace TeaLearning.Services.Interfaces;

public interface ITeacherAnalyticsService
{
    Task<ApiResponse<TeacherStudentSummaryDto>> GetStudentAnalyticsSummaryAsync(
        Guid studentId, 
        CancellationToken cancellationToken = default);

    Task<ApiResponse<TeacherClassroomSummaryDto>> GetClassroomAnalyticsSummaryAsync(
        Guid classroomId, 
        CancellationToken cancellationToken = default);
}
