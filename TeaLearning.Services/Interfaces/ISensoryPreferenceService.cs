using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Sensory;

namespace TeaLearning.Services.Interfaces;

public interface ISensoryPreferenceService
{
    Task<ApiResponse<SensoryPreferenceDto>> GetSensoryPreferencesByStudentIdAsync(
        Guid studentId, 
        CancellationToken cancellationToken = default);

    Task<ApiResponse<SensoryPreferenceDto>> UpdateSensoryPreferencesAsync(
        Guid studentId, 
        UpdateSensoryPreferenceDto dto, 
        CancellationToken cancellationToken = default);
}
