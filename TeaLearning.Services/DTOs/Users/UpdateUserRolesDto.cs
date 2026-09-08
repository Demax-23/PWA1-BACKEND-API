using System.ComponentModel.DataAnnotations;

namespace TeaLearning.Services.DTOs.Users;

public class UpdateUserRolesDto
{
    [Required(ErrorMessage = "Debe especificar al menos un rol")]
    public List<string> Roles { get; set; } = new List<string>();
}
