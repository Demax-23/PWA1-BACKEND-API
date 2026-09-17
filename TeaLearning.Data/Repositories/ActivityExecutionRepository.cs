using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Data.Context;

namespace TeaLearning.Data.Repositories;

public class ActivityExecutionRepository : GenericRepository<ActivityExecution>, IActivityExecutionRepository
{
    public ActivityExecutionRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<ActivityExecution>> GetByAssignmentIdAsync(Guid assignmentId, CancellationToken cancellationToken = default)
    {
        return await _context.ActivityExecutions
            .Where(e => e.AssignmentId == assignmentId && e.IsActive)
            .OrderByDescending(e => e.FechaRegistro)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ActivityExecution>> GetByStudentIdAsync(Guid studentProfileId, CancellationToken cancellationToken = default)
    {
        return await _context.ActivityExecutions
            .Include(e => e.Assignment)
                .ThenInclude(a => a.Activity)
            .Where(e => e.StudentProfileId == studentProfileId && e.IsActive)
            .OrderByDescending(e => e.FechaRegistro)
            .ToListAsync(cancellationToken);
    }
}
