using System.ComponentModel.DataAnnotations;
using System.Text;

namespace TeaLearning.Services.DTOs.Auth;

public class RegisterRequestDto : IValidatableObject
{
    [Required(ErrorMessage = "El correo electrónico es obligatorio")]
    [EmailAddress(ErrorMessage = "Formato de correo electrónico inválido")]
    [MaxLength(256, ErrorMessage = "El correo electrónico no puede exceder los 256 caracteres")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria")]
    [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres")]
    [MaxLength(72, ErrorMessage = "La contraseña no puede superar los 72 caracteres")]
    [RegularExpression(@"^.*\d.*$", ErrorMessage = "La contraseña debe incluir al menos un número")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [MaxLength(100, ErrorMessage = "Los nombres no pueden exceder los 100 caracteres")]
    public string Nombres { get; set; } = string.Empty;

    [Required(ErrorMessage = "Los apellidos son obligatorios")]
    [MaxLength(100, ErrorMessage = "Los apellidos no pueden exceder los 100 caracteres")]
    public string Apellidos { get; set; } = string.Empty;

    [Required(ErrorMessage = "El rol es obligatorio")]
    [MaxLength(30)]
    public string Rol { get; set; } = string.Empty; // Administrador, Docente, Estudiante, Acudiente

    // Campos opcionales según el rol
    [MaxLength(30)]
    public string? Telefono { get; set; }

    [MaxLength(50)]
    public string? Parentesco { get; set; }

    [MaxLength(100)]
    public string? Especialidad { get; set; }

    [MaxLength(50)]
    public string? CodigoEstudiante { get; set; }

    public DateTime? FechaNacimiento { get; set; }
    public Guid? ClassroomId { get; set; }
    public Guid? GuardianProfileId { get; set; }
    public List<Guid>? StudentIds { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrEmpty(Password) && Encoding.UTF8.GetByteCount(Password) > 72)
        {
            yield return new ValidationResult("La contraseña supera el límite máximo permitido de 72 bytes.", [nameof(Password)]);
        }
    }
}
