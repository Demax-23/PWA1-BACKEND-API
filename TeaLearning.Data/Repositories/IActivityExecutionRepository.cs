using TeaLearning.Core.Entities;

namespace TeaLearning.Data.Repositories;

public interface IActivityExecutionRepository : IGenericRepository<ActivityExecution>
{
    Task<IReadOnlyList<ActivityExecution>> GetByAssignmentIdAsync(Guid assignmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActivityExecution>> GetByStudentIdAsync(Guid studentProfileId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActivityExecution>> GetByStudentIdWithActivityDetailsAsync(Guid studentProfileId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActivityExecution>> GetByClassroomIdWithDetailsAsync(Guid classroomId, CancellationToken cancellationToken = default);
}
