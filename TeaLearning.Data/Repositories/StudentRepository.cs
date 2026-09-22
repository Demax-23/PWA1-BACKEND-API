using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Data.Context;

namespace TeaLearning.Data.Repositories;

public class StudentRepository : GenericRepository<StudentProfile>, IStudentRepository
{
    public StudentRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.StudentProfiles
            .Include(s => s.User)
            .Include(s => s.SensoryPreference)
            .Include(s => s.Classroom)
            .Include(s => s.GuardianProfile)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive, cancellationToken);
    }

    public async Task<StudentProfile?> GetStudentWithDetailsAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        return await _context.StudentProfiles
            .Include(s => s.User)
            .Include(s => s.SensoryPreference)
            .Include(s => s.Classroom)
            .Include(s => s.GuardianProfile)
                .ThenInclude(g => g!.User)
            .FirstOrDefaultAsync(s => s.Id == studentId && s.IsActive, cancellationToken);
    }

    public async Task<List<StudentProfile>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.StudentProfiles
            .Include(s => s.User)
            .Include(s => s.SensoryPreference)
            .Include(s => s.Classroom)
            .Include(s => s.GuardianProfile)
                .ThenInclude(g => g!.User)
            .Where(s => s.IsActive)
            .OrderBy(s => s.User.Nombres)
            .ToListAsync(cancellationToken);
    }
}
