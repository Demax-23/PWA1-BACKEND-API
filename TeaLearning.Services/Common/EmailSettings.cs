namespace TeaLearning.Services.Common;

/// <summary>
/// Configuración SMTP para el envío de correos transaccionales (sección "Email").
/// La contraseña (Password) NUNCA debe versionarse: se provee con user-secrets o variable de entorno Email__Password.
/// </summary>
public class EmailSettings
{
    public const string SectionName = "Email";

    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string SenderName { get; set; } = "Sendero · TeaLearning";
    public string SenderAddress { get; set; } = string.Empty;
}
