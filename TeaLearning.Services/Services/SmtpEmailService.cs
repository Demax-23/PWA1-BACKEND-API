using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using TeaLearning.Services.Common;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Services.Services;

/// <summary>
/// Envío de correos por SMTP (Gmail: smtp.gmail.com:587 con STARTTLS) usando MailKit.
/// El remitente siempre es la cuenta autenticada, porque Gmail reescribe cualquier otro "From".
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailSettings> settings, ILogger<SmtpEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        string toAddress,
        string toName,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.Password) || string.IsNullOrWhiteSpace(_settings.Username))
        {
            throw new InvalidOperationException(
                "El servicio de correo no está configurado. Defina 'Email:Username' y 'Email:Password' (user-secrets o variable de entorno Email__Password).");
        }

        var senderAddress = string.IsNullOrWhiteSpace(_settings.SenderAddress) ? _settings.Username : _settings.SenderAddress;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.SenderName, senderAddress));
        message.To.Add(new MailboxAddress(toName, toAddress));
        message.Subject = subject;
        message.Body = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = textBody
        }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        // No se registra la dirección completa para no dejar datos personales en los logs.
        _logger.LogInformation("Correo '{Subject}' enviado correctamente.", subject);
    }
}
