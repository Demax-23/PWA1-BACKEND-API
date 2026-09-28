using TeaLearning.Core.Entities;

namespace TeaLearning.Data.Repositories;

public interface IPedagogicalNoteRepository : IGenericRepository<PedagogicalNote>
{
    Task<IReadOnlyList<PedagogicalNote>> GetByStudentIdAsync(
        Guid studentProfileId, 
        CancellationToken cancellationToken = default);

    Task<PedagogicalNote?> GetWithDetailsAsync(
        Guid id, 
        CancellationToken cancellationToken = default);
}
