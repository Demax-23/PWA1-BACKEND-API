using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;

namespace TeaLearning.Data.Repositories;

public interface IActivityAssignmentRepository : IGenericRepository<ActivityAssignment>
{
    Task<IReadOnlyList<ActivityAssignment>> GetDailyRoutineByStudentIdAsync(
        Guid studentProfileId, 
        DateTime date, 
        CancellationToken cancellationToken = default);

    Task<ActivityAssignment?> GetWithDetailsAsync(
        Guid id, 
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActivityAssignment>> GetFilteredAsync(
        Guid? studentProfileId = null,
        Guid? classroomId = null,
        EstadoAsignacion? estado = null,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(
        IEnumerable<ActivityAssignment> assignments, 
        CancellationToken cancellationToken = default);
}
