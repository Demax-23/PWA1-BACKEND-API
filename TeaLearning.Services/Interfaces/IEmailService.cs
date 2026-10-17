namespace TeaLearning.Services.Interfaces;

/// <summary>
/// Abstracción para el envío de correos transaccionales (restablecimiento y confirmación de contraseña).
/// </summary>
public interface IEmailService
{
    Task SendAsync(
        string toAddress,
        string toName,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default);
}
