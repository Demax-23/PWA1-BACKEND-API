using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;

namespace TeaLearning.Data.Repositories;

public interface IActivityRepository : IGenericRepository<Activity>
{
    Task<Activity?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Activity>> GetFilteredAsync(
        Guid? moduloId = null, 
        TipoActividad? tipoActividad = null, 
        Guid? docenteId = null, 
        CancellationToken cancellationToken = default);
}
