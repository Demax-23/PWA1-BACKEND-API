using System.ComponentModel.DataAnnotations;

namespace TeaLearning.Services.DTOs.Users;

public class UpdateUserDto
{
    [MaxLength(100, ErrorMessage = "Los nombres no pueden exceder 100 caracteres")]
    public string? Nombres { get; set; }

    [MaxLength(100, ErrorMessage = "Los apellidos no pueden exceder 100 caracteres")]
    public string? Apellidos { get; set; }

    public bool? IsActive { get; set; }

    public List<string>? Roles { get; set; }
}
