using Microsoft.AspNetCore.Identity;

namespace TeaLearning.Core.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string NombreCompleto => $"{Nombres} {Apellidos}".Trim();
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UltimaActividadAt { get; set; }

    // Relaciones 1:1 opcionales con perfiles de rol
    public virtual StudentProfile? StudentProfile { get; set; }
    public virtual TeacherProfile? TeacherProfile { get; set; }
    public virtual GuardianProfile? GuardianProfile { get; set; }
}
