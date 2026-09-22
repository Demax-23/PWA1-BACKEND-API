using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Users;

namespace TeaLearning.Services.Interfaces;

public interface IUserService
{
    Task<ApiResponse<List<UserDto>>> GetAllUsersAsync();
    Task<ApiResponse<UserDto>> GetUserByIdAsync(Guid id);
    Task<ApiResponse<UserDto>> UpdateUserAsync(Guid id, UpdateUserDto dto);
    Task<ApiResponse<UserDto>> UpdateUserRolesAsync(Guid id, UpdateUserRolesDto dto);
    Task<ApiResponse<bool>> DeleteUserAsync(Guid id);
    Task<ApiResponse<List<Guid>>> LinkStudentsToGuardianAsync(Guid guardianUserId, List<Guid> studentIds);
}
