using TeaLearning.Core.Entities;

namespace TeaLearning.Data.Repositories;

public interface ISensoryPreferenceRepository : IGenericRepository<SensoryPreference>
{
    Task<SensoryPreference?> GetByStudentIdAsync(Guid studentId, CancellationToken cancellationToken = default);
}
