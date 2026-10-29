using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Data.Context;

namespace TeaLearning.Data.Repositories;

public class ActivityRepository : GenericRepository<Activity>, IActivityRepository
{
    public ActivityRepository(ApplicationDbContext context) : base(context)
    {
    }

    public override async Task DeleteAsync(Activity entity, CancellationToken cancellationToken = default)
    {
        var assignments = await _context.ActivityAssignments
            .Where(aa => aa.ActivityId == entity.Id)
            .ToListAsync(cancellationToken);

        if (assignments.Count > 0)
        {
            var assignmentIds = assignments.Select(a => a.Id).ToList();

            var executions = await _context.ActivityExecutions
                .Where(ae => assignmentIds.Contains(ae.AssignmentId))
                .ToListAsync(cancellationToken);

            if (executions.Count > 0)
            {
                _context.ActivityExecutions.RemoveRange(executions);
            }

            var notes = await _context.PedagogicalNotes
                .Where(pn => pn.AssignmentId != null && assignmentIds.Contains(pn.AssignmentId.Value))
                .ToListAsync(cancellationToken);

            foreach (var note in notes)
            {
                note.AssignmentId = null;
            }

            _context.ActivityAssignments.RemoveRange(assignments);
        }

        _context.Activities.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Activity?> GetByIdWithDetailsAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Activities
            .Include(a => a.Modulo)
            .Include(a => a.Docente)
                .ThenInclude(d => d.User)
            .Where(a => a.Id == id);

        if (!includeInactive)
        {
            query = query.Where(a => a.IsActive);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Activity>> GetFilteredAsync(
        Guid? moduloId = null, 
        TipoActividad? tipoActividad = null, 
        Guid? docenteId = null, 
        bool onlyActive = true,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Activities
            .Include(a => a.Modulo)
            .Include(a => a.Docente)
                .ThenInclude(d => d.User)
            .AsQueryable();

        if (onlyActive)
        {
            query = query.Where(a => a.IsActive);
        }

        if (moduloId.HasValue)
        {
            query = query.Where(a => a.ModuloId == moduloId.Value);
        }

        if (tipoActividad.HasValue)
        {
            query = query.Where(a => a.TipoActividad == tipoActividad.Value);
        }

        if (docenteId.HasValue)
        {
            query = query.Where(a => a.DocenteId == docenteId.Value);
        }

        return await query.OrderByDescending(a => a.CreatedAt).ToListAsync(cancellationToken);
    }
}
