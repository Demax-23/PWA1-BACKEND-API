using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Data.Context;

namespace TeaLearning.Data.Repositories;

public class ActivityAssignmentRepository : GenericRepository<ActivityAssignment>, IActivityAssignmentRepository
{
    public ActivityAssignmentRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<ActivityAssignment>> GetDailyRoutineByStudentIdAsync(
        Guid studentProfileId, 
        DateTime date, 
        CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;

        return await _context.ActivityAssignments
            .Include(aa => aa.Activity)
                .ThenInclude(a => a.Modulo)
            .Where(aa => aa.StudentProfileId == studentProfileId && aa.IsActive)
            .Where(aa => aa.FechaInicio.Date <= targetDate && 
                        (aa.FechaLimite == null || aa.FechaLimite.Value.Date >= targetDate))
            .OrderBy(aa => aa.OrdenSecuencial)
            .ToListAsync(cancellationToken);
    }
}
