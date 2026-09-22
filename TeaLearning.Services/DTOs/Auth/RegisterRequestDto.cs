using System.ComponentModel.DataAnnotations;

namespace TeaLearning.Services.DTOs.Auth;

public class RegisterRequestDto
{
    [Required(ErrorMessage = "El correo electrónico es obligatorio")]
    [EmailAddress(ErrorMessage = "Formato de correo electrónico inválido")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria")]
    [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio")]
    public string Nombres { get; set; } = string.Empty;

    [Required(ErrorMessage = "Los apellidos son obligatorios")]
    public string Apellidos { get; set; } = string.Empty;

    [Required(ErrorMessage = "El rol es obligatorio")]
    public string Rol { get; set; } = string.Empty; // Administrador, Docente, Estudiante, Acudiente

    // Campos opcionales según el rol
    public string? Telefono { get; set; }
    public string? Parentesco { get; set; }
    public string? Especialidad { get; set; }
    public string? CodigoEstudiante { get; set; }
    public DateTime? FechaNacimiento { get; set; }
    public Guid? ClassroomId { get; set; }
    public Guid? GuardianProfileId { get; set; }
    public List<Guid>? StudentIds { get; set; }
}
