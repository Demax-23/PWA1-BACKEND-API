using TeaLearning.Core.Entities;

namespace TeaLearning.Data.Repositories;

public interface ILearningCategoryRepository : IGenericRepository<LearningCategory>
{
    Task<IReadOnlyList<LearningCategory>> GetAllWithModulesCountAsync(bool onlyActive = true, CancellationToken cancellationToken = default);
    Task<LearningCategory?> GetWithModulesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> HasAssociatedModulesAsync(Guid id, bool onlyActive = true, CancellationToken cancellationToken = default);
    Task DisassociateInactiveModulesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string nombre, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
