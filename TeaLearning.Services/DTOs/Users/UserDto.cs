namespace TeaLearning.Services.DTOs.Users;

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new List<string>();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UltimaActividadAt { get; set; }
    public string? AvatarUrl { get; set; }

    // Identificadores de perfiles asociados si aplican
    public Guid? EstudianteId { get; set; }
    public Guid? DocenteId { get; set; }
    public Guid? AcudienteId { get; set; }

    // Estudiantes a cargo si el usuario tiene rol Acudiente
    public List<Guid> StudentUserIds { get; set; } = new List<Guid>();
    public List<string> StudentNombres { get; set; } = new List<string>();
}
