using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Modules;

namespace TeaLearning.Services.Interfaces;

public interface IModuleService
{
    Task<ApiResponse<List<ModuleDto>>> GetAllModulesAsync(bool onlyActive = true, CancellationToken cancellationToken = default);
    Task<ApiResponse<ModuleDetailDto>> GetModuleByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiResponse<ModuleDto>> CreateModuleAsync(CreateModuleDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse<ModuleDto>> UpdateModuleAsync(Guid id, UpdateModuleDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteModuleAsync(Guid id, CancellationToken cancellationToken = default);
}
