using TeaLearning.Core.Common;
using TeaLearning.Core.Enums;

namespace TeaLearning.Core.Entities;

public class Activity : BaseEntity
{
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public TipoActividad TipoActividad { get; set; } = TipoActividad.SecuenciaLogica;
    public string ObjetivoPedagogico { get; set; } = string.Empty;

    // Configuración dinámica del ejercicio (JSON sin alterar la BD física)
    public string ConfigJson { get; set; } = "{}";

    public Guid ModuloId { get; set; }
    public virtual LearningModule Modulo { get; set; } = null!;

    public Guid DocenteId { get; set; }
    public virtual TeacherProfile Docente { get; set; } = null!;

    public virtual ICollection<ActivityAssignment> Assignments { get; set; } = new List<ActivityAssignment>();
}
