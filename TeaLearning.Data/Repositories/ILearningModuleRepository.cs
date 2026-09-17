using TeaLearning.Core.Entities;

namespace TeaLearning.Data.Repositories;

public interface ILearningModuleRepository : IGenericRepository<LearningModule>
{
    Task<IReadOnlyList<LearningModule>> GetAllWithActivitiesCountAsync(CancellationToken cancellationToken = default);
    Task<LearningModule?> GetWithActivitiesAsync(Guid id, CancellationToken cancellationToken = default);
}
