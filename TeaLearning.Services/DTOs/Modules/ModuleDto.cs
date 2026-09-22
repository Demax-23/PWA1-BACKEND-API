namespace TeaLearning.Services.DTOs.Modules;

public class ModuleDto
{
    public Guid Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? Icono { get; set; }
    public int Orden { get; set; }
    public Guid? CategoriaId { get; set; }
    public string? CategoriaNombre { get; set; }
    public int TotalActividades { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
