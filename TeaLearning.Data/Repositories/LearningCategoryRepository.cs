using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Data.Context;

namespace TeaLearning.Data.Repositories;

public class LearningCategoryRepository : GenericRepository<LearningCategory>, ILearningCategoryRepository
{
    public LearningCategoryRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<LearningCategory>> GetAllWithModulesCountAsync(bool onlyActive = true, CancellationToken cancellationToken = default)
    {
        IQueryable<LearningCategory> query = _context.LearningCategories
            .Include(c => c.Modules);

        if (onlyActive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<LearningCategory?> GetWithModulesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.LearningCategories
            .Include(c => c.Modules)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<bool> HasAssociatedModulesAsync(Guid id, bool onlyActive = true, CancellationToken cancellationToken = default)
    {
        IQueryable<LearningModule> query = _context.LearningModules
            .Where(m => m.CategoriaId == id);

        if (onlyActive)
        {
            query = query.Where(m => m.IsActive);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task DisassociateInactiveModulesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var inactiveModules = await _context.LearningModules
            .Where(m => m.CategoriaId == id && !m.IsActive)
            .ToListAsync(cancellationToken);

        if (inactiveModules.Any())
        {
            foreach (var m in inactiveModules)
            {
                m.CategoriaId = null;
            }
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public override async Task DeleteAsync(LearningCategory entity, CancellationToken cancellationToken = default)
    {
        _context.LearningCategories.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(string nombre, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalized = nombre.Trim().ToLower();
        return await _context.LearningCategories
            .AnyAsync(c => c.Nombre.ToLower() == normalized && (!excludeId.HasValue || c.Id != excludeId.Value), cancellationToken);
    }
}
