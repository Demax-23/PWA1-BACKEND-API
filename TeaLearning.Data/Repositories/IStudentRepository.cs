using TeaLearning.Core.Entities;

namespace TeaLearning.Data.Repositories;

public interface IStudentRepository : IGenericRepository<StudentProfile>
{
    Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<StudentProfile?> GetStudentWithDetailsAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<List<StudentProfile>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default);
}
