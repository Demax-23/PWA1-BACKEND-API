using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Data.Context;

namespace TeaLearning.Data.Repositories;

public class LearningModuleRepository : GenericRepository<LearningModule>, ILearningModuleRepository
{
    public LearningModuleRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<LearningModule>> GetAllWithActivitiesCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.LearningModules
            .Include(m => m.Categoria)
            .Include(m => m.Activities.Where(a => a.IsActive))
            .Where(m => m.IsActive)
            .OrderBy(m => m.Orden)
            .ToListAsync(cancellationToken);
    }

    public async Task<LearningModule?> GetWithActivitiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.LearningModules
            .Include(m => m.Categoria)
            .Include(m => m.Activities.Where(a => a.IsActive))
            .FirstOrDefaultAsync(m => m.Id == id && m.IsActive, cancellationToken);
    }

    public async Task ShiftOrdersAsync(Guid categoriaId, int fromOrder, int delta, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.LearningModules.Where(m => m.CategoriaId == categoriaId && m.Orden >= fromOrder);
        if (excludeId.HasValue)
        {
            query = query.Where(m => m.Id != excludeId.Value);
        }

        var modules = await query.ToListAsync(cancellationToken);
        if (modules.Any())
        {
            foreach (var mod in modules.OrderByDescending(m => m.Orden))
            {
                mod.Orden += delta;
                mod.UpdatedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ReorderOnUpdateAsync(Guid categoriaId, int oldOrder, int newOrder, Guid currentId, CancellationToken cancellationToken = default)
    {
        if (oldOrder == newOrder) return;

        IQueryable<LearningModule> query;
        int delta;
        if (newOrder < oldOrder)
        {
            query = _context.LearningModules.Where(m => m.CategoriaId == categoriaId && m.Id != currentId && m.Orden >= newOrder && m.Orden < oldOrder);
            delta = 1;
        }
        else
        {
            query = _context.LearningModules.Where(m => m.CategoriaId == categoriaId && m.Id != currentId && m.Orden <= newOrder && m.Orden > oldOrder);
            delta = -1;
        }

        var items = await query.ToListAsync(cancellationToken);
        if (items.Any())
        {
            foreach (var item in items)
            {
                item.Orden += delta;
                item.UpdatedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
