using TeaLearning.Core.Entities;

namespace TeaLearning.Data.Repositories;

public interface IActivityAssignmentRepository : IGenericRepository<ActivityAssignment>
{
    Task<IReadOnlyList<ActivityAssignment>> GetDailyRoutineByStudentIdAsync(
        Guid studentProfileId, 
        DateTime date, 
        CancellationToken cancellationToken = default);
}
