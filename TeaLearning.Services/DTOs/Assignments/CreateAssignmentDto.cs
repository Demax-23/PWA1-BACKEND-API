using System.ComponentModel.DataAnnotations;
using TeaLearning.Core.Enums;

namespace TeaLearning.Services.DTOs.Assignments;

public class CreateAssignmentDto
{
    [Required(ErrorMessage = "El identificador de la actividad es obligatorio")]
    public Guid ActivityId { get; set; }

    // Asignación individual (Opcional si se especifica ClassroomId)
    public Guid? StudentProfileId { get; set; }

    // Asignación masiva para toda un aula (Opcional si se especifica StudentProfileId)
    public Guid? ClassroomId { get; set; }

    public DateTime FechaInicio { get; set; } = DateTime.UtcNow.Date;
    public DateTime? FechaLimite { get; set; }

    [Range(1, 100, ErrorMessage = "El orden secuencial debe ser mayor o igual a 1")]
    public int OrdenSecuencial { get; set; } = 1;

    public PrioridadActividad Prioridad { get; set; } = PrioridadActividad.Media;
}
