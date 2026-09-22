using System.ComponentModel.DataAnnotations;

namespace TeaLearning.Services.DTOs.Users;

public class LinkStudentsDto
{
    [Required(ErrorMessage = "La lista de identificadores de estudiantes es obligatoria")]
    public List<Guid> StudentIds { get; set; } = new List<Guid>();
}
