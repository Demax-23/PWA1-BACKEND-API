using TeaLearning.Core.Common;

namespace TeaLearning.Core.Entities;

public class LearningCategory : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Color { get; set; } = "#2F6E5C";
    public int Orden { get; set; } = 1;

    public virtual ICollection<LearningModule> Modules { get; set; } = new List<LearningModule>();
}
