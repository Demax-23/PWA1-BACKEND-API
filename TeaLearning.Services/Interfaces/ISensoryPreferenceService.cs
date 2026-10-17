using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Sensory;

namespace TeaLearning.Services.Interfaces;

public interface ISensoryPreferenceService
{
    Task<ApiResponse<SensoryPreferenceDto>> GetSensoryPreferencesByStudentIdAsync(
        Guid studentId, 
        Guid? currentUserId = null,
        bool isStaff = true,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<SensoryPreferenceDto>> UpdateSensoryPreferencesAsync(
        Guid studentId, 
        UpdateSensoryPreferenceDto dto, 
        Guid? currentUserId = null,
        bool isAdmin = true,
        CancellationToken cancellationToken = default);
}
