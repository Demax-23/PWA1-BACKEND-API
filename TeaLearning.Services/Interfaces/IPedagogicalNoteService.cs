using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Pedagogical;

namespace TeaLearning.Services.Interfaces;

public interface IPedagogicalNoteService
{
    Task<ApiResponse<PedagogicalNoteDto>> CreateNoteAsync(
        Guid studentId, 
        Guid currentUserId, 
        CreatePedagogicalNoteDto dto, 
        CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyList<PedagogicalNoteDto>>> GetNotesByStudentIdAsync(
        Guid studentId, 
        Guid currentUserId, 
        bool isStaff, 
        CancellationToken cancellationToken = default);

    Task<ApiResponse<PedagogicalNoteDto>> UpdateNoteAsync(
        Guid noteId,
        Guid currentUserId,
        bool isAdmin,
        CreatePedagogicalNoteDto dto,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> DeleteNoteAsync(
        Guid noteId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default);
}
