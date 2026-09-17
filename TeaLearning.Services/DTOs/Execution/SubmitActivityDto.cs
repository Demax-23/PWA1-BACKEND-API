using System.ComponentModel.DataAnnotations;

namespace TeaLearning.Services.DTOs.Execution;

public class SubmitActivityDto
{
    [Range(0, 7200, ErrorMessage = "El tiempo de respuesta debe ser un valor positivo válido")]
    public int TiempoRespuestaSegundos { get; set; }

    [Range(0, 1000, ErrorMessage = "La cantidad de intentos fallidos debe ser mayor o igual a 0")]
    public int CantidadIntentosFallidos { get; set; }

    [Range(0, 1000, ErrorMessage = "La cantidad de aciertos debe ser mayor o igual a 0")]
    public int CantidadAciertos { get; set; }

    public bool CompletadaExitosamente { get; set; } = true;
}
