namespace TeaLearning.Services.DTOs.Students;

public class StudentProfileDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string CodigoEstudiante { get; set; } = string.Empty;
    public DateTime FechaNacimiento { get; set; }
    public string? ObservacionesMedicasTEA { get; set; }
    public Guid? ClassroomId { get; set; }
    public string? ClassroomNombre { get; set; }
    public Guid? GuardianProfileId { get; set; }
    public string? GuardianNombre { get; set; }
}
