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
            .Include(m => m.Activities.Where(a => a.IsActive))
            .Where(m => m.IsActive)
            .OrderBy(m => m.Orden)
            .ToListAsync(cancellationToken);
    }

    public async Task<LearningModule?> GetWithActivitiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.LearningModules
            .Include(m => m.Activities.Where(a => a.IsActive))
            .FirstOrDefaultAsync(m => m.Id == id && m.IsActive, cancellationToken);
    }
}
