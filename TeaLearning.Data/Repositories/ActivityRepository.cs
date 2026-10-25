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
