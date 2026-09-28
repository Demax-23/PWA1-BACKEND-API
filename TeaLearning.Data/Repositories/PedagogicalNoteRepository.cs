using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Data.Context;

namespace TeaLearning.Data.Repositories;

public class PedagogicalNoteRepository : GenericRepository<PedagogicalNote>, IPedagogicalNoteRepository
{
    public PedagogicalNoteRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<PedagogicalNote>> GetByStudentIdAsync(
        Guid studentProfileId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.PedagogicalNotes
            .Include(n => n.TeacherProfile)
                .ThenInclude(t => t.User)
            .Where(n => n.StudentProfileId == studentProfileId && n.IsActive)
            .OrderByDescending(n => n.FechaRegistro)
            .ToListAsync(cancellationToken);
    }

    public async Task<PedagogicalNote?> GetWithDetailsAsync(
        Guid id, 
        CancellationToken cancellationToken = default)
    {
        return await _context.PedagogicalNotes
            .Include(n => n.TeacherProfile)
                .ThenInclude(t => t.User)
            .Include(n => n.StudentProfile)
                .ThenInclude(s => s.User)
            .FirstOrDefaultAsync(n => n.Id == id && n.IsActive, cancellationToken);
    }
}
