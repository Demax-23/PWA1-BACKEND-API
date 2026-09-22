using TeaLearning.Core.Common;

namespace TeaLearning.Core.Entities;

public class LearningModule : BaseEntity
{
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? Icono { get; set; }
    public int Orden { get; set; } = 1;

    public Guid? CategoriaId { get; set; }
    public virtual LearningCategory? Categoria { get; set; }

    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
}
