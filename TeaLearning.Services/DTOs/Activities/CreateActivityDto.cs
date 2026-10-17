using System.ComponentModel.DataAnnotations;
using TeaLearning.Core.Enums;

namespace TeaLearning.Services.DTOs.Activities;

public class CreateActivityDto
{
    [Required(ErrorMessage = "El título de la actividad es obligatorio")]
    [MaxLength(200, ErrorMessage = "El título no puede superar los 200 caracteres")]
    public string Titulo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción de la actividad es obligatoria")]
    [MaxLength(1000, ErrorMessage = "La descripción no puede superar los 1000 caracteres")]
    public string Descripcion { get; set; } = string.Empty;

    [Required(ErrorMessage = "El tipo de actividad es obligatorio")]
    public TipoActividad TipoActividad { get; set; } = TipoActividad.SecuenciaLogica;

    [Required(ErrorMessage = "El objetivo pedagógico es obligatorio")]
    [MaxLength(500, ErrorMessage = "El objetivo pedagógico no puede superar los 500 caracteres")]
    public string ObjetivoPedagogico { get; set; } = string.Empty;

    [Required(ErrorMessage = "La estructura dinámica de configuración JSON es obligatoria")]
    [MaxLength(16384, ErrorMessage = "La estructura ConfigJson no puede superar los 16 KB (16384 caracteres)")]
    public string ConfigJson { get; set; } = "{}";

    [Required(ErrorMessage = "El identificador del módulo temático es obligatorio")]
    public Guid ModuloId { get; set; }
}
