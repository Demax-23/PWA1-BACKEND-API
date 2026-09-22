using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
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

    public async Task<ActivityAssignment?> GetWithDetailsAsync(
        Guid id, 
        CancellationToken cancellationToken = default)
    {
        return await _context.ActivityAssignments
            .Include(aa => aa.Activity)
                .ThenInclude(a => a.Modulo)
            .Include(aa => aa.StudentProfile)
                .ThenInclude(s => s.User)
            .Include(aa => aa.StudentProfile)
                .ThenInclude(s => s.SensoryPreference)
            .Include(aa => aa.Executions)
            .FirstOrDefaultAsync(aa => aa.Id == id && aa.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyList<ActivityAssignment>> GetFilteredAsync(
        Guid? studentProfileId = null, 
        Guid? classroomId = null, 
        EstadoAsignacion? estado = null, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.ActivityAssignments
            .Include(aa => aa.Activity)
                .ThenInclude(a => a.Modulo)
            .Include(aa => aa.StudentProfile)
                .ThenInclude(s => s.User)
            .Where(aa => aa.IsActive && aa.Activity.IsActive);

        if (studentProfileId.HasValue)
        {
            query = query.Where(aa => aa.StudentProfileId == studentProfileId.Value);
        }

        if (classroomId.HasValue)
        {
            query = query.Where(aa => aa.StudentProfile.ClassroomId == classroomId.Value);
        }

        if (estado.HasValue)
        {
            query = query.Where(aa => aa.Estado == estado.Value);
        }

        return await query.OrderBy(aa => aa.OrdenSecuencial).ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(
        IEnumerable<ActivityAssignment> assignments, 
        CancellationToken cancellationToken = default)
    {
        await _context.ActivityAssignments.AddRangeAsync(assignments, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
