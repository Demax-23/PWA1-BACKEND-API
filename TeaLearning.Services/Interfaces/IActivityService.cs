using TeaLearning.Core.Enums;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Activities;

namespace TeaLearning.Services.Interfaces;

public interface IActivityService
{
    Task<ApiResponse<List<ActivityDto>>> GetActivitiesAsync(
        Guid? moduloId = null, 
        TipoActividad? tipoActividad = null, 
        Guid? docenteId = null, 
        CancellationToken cancellationToken = default);

    Task<ApiResponse<ActivityDto>> GetActivityByIdAsync(
        Guid id, 
        CancellationToken cancellationToken = default);

    Task<ApiResponse<ActivityDto>> CreateActivityAsync(
        Guid teacherUserId, 
        CreateActivityDto dto, 
        CancellationToken cancellationToken = default);

    Task<ApiResponse<ActivityDto>> UpdateActivityAsync(
        Guid id, 
        Guid teacherUserId, 
        UpdateActivityDto dto, 
        bool isAdmin = false, 
        CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> DeleteActivityAsync(
        Guid id, 
        Guid teacherUserId, 
        bool isAdmin = false, 
        CancellationToken cancellationToken = default);
}
