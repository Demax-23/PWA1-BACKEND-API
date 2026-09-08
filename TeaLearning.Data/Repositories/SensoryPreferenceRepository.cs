using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Data.Context;

namespace TeaLearning.Data.Repositories;

public class SensoryPreferenceRepository : GenericRepository<SensoryPreference>, ISensoryPreferenceRepository
{
    public SensoryPreferenceRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<SensoryPreference?> GetByStudentIdAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        return await _context.SensoryPreferences
            .Include(sp => sp.StudentProfile)
            .FirstOrDefaultAsync(sp => sp.StudentProfileId == studentId && sp.IsActive, cancellationToken);
    }
}
