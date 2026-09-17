using TeaLearning.Core.Enums;

namespace TeaLearning.Services.DTOs.Activities;

public class ActivityDto
{
    public Guid Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public TipoActividad TipoActividad { get; set; }
    public string TipoActividadNombre => TipoActividad.ToString();
    public string ObjetivoPedagogico { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = "{}";
    public Guid ModuloId { get; set; }
    public string ModuloTitulo { get; set; } = string.Empty;
    public Guid DocenteId { get; set; }
    public string DocenteNombre { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
