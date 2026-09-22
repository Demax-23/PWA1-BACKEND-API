namespace TeaLearning.Services.DTOs.Categories;

public class CategoryDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Color { get; set; } = "#2F6E5C";
    public int Orden { get; set; }
    public int TotalModulos { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
