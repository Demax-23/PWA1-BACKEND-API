using TeaLearning.Core.Entities;

namespace TeaLearning.Data.Repositories;

public interface ILearningModuleRepository : IGenericRepository<LearningModule>
{
    Task<IReadOnlyList<LearningModule>> GetAllWithActivitiesCountAsync(bool onlyActive = true, CancellationToken cancellationToken = default);
    Task<LearningModule?> GetWithActivitiesAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task ShiftOrdersAsync(Guid categoriaId, int fromOrder, int delta, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task ReorderOnUpdateAsync(Guid categoriaId, int oldOrder, int newOrder, Guid currentId, CancellationToken cancellationToken = default);
}
