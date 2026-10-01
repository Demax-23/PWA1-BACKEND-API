using System.ComponentModel.DataAnnotations;

namespace TeaLearning.Services.DTOs.Users;

public class UpdateUserDto
{
    [MaxLength(100, ErrorMessage = "Los nombres no pueden exceder 100 caracteres")]
    public string? Nombres { get; set; }

    [MaxLength(100, ErrorMessage = "Los apellidos no pueden exceder 100 caracteres")]
    public string? Apellidos { get; set; }

    [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido")]
    [MaxLength(256, ErrorMessage = "El correo no puede exceder 256 caracteres")]
    public string? Email { get; set; }

    public bool? IsActive { get; set; }

    public List<string>? Roles { get; set; }
}
