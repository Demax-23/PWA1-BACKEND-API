using System.Net;

namespace TeaLearning.Services.Services;

/// <summary>
/// Plantillas de correo con tono calmado y lenguaje literal, acorde al público de la plataforma.
/// Todo contenido dinámico se codifica en HTML para evitar inyección.
/// </summary>
public static class PasswordResetEmailTemplates
{
    public static (string Subject, string Html, string Text) BuildResetLink(string fullName, string link, int validMinutes)
    {
        var name = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(fullName) ? "usuario" : fullName);
        var safeLink = WebUtility.HtmlEncode(link);

        var html = $@"<!DOCTYPE html>
<html lang=""es"">
<body style=""margin:0;padding:24px;background:#F4F6F5;font-family:Arial,Helvetica,sans-serif;color:#202623;"">
  <div style=""max-width:520px;margin:0 auto;background:#FFFFFF;border:1px solid #D6DDD9;border-radius:14px;padding:32px;"">
    <h1 style=""font-size:20px;margin:0 0 16px 0;"">Restablecer tu contraseña</h1>
    <p style=""font-size:16px;line-height:1.6;margin:0 0 12px 0;"">Hola {name}.</p>
    <p style=""font-size:16px;line-height:1.6;margin:0 0 20px 0;"">Recibimos una solicitud para cambiar tu contraseña en Sendero. Pulsa el botón para elegir una nueva.</p>
    <p style=""margin:0 0 20px 0;""><a href=""{safeLink}"" style=""display:inline-block;background:#255A4B;color:#FFFFFF;text-decoration:none;font-weight:bold;font-size:16px;padding:14px 22px;border-radius:8px;"">Cambiar mi contraseña</a></p>
    <p style=""font-size:14px;line-height:1.6;color:#58615C;margin:0 0 8px 0;"">Este enlace funciona una sola vez y vence en {validMinutes} minutos.</p>
    <p style=""font-size:14px;line-height:1.6;color:#58615C;margin:0;"">Si no pediste este cambio, ignora este mensaje. Tu contraseña actual seguirá funcionando.</p>
  </div>
</body>
</html>";

        var text =
$@"Hola {fullName}.

Recibimos una solicitud para cambiar tu contraseña en Sendero.
Abre este enlace para elegir una nueva:

{link}

Este enlace funciona una sola vez y vence en {validMinutes} minutos.
Si no pediste este cambio, ignora este mensaje. Tu contraseña actual seguirá funcionando.";

        return ("Restablece tu contraseña · Sendero", html, text);
    }

    public static (string Subject, string Html, string Text) BuildPasswordChanged(string fullName)
    {
        var name = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(fullName) ? "usuario" : fullName);

        var html = $@"<!DOCTYPE html>
<html lang=""es"">
<body style=""margin:0;padding:24px;background:#F4F6F5;font-family:Arial,Helvetica,sans-serif;color:#202623;"">
  <div style=""max-width:520px;margin:0 auto;background:#FFFFFF;border:1px solid #D6DDD9;border-radius:14px;padding:32px;"">
    <h1 style=""font-size:20px;margin:0 0 16px 0;"">Tu contraseña fue modificada</h1>
    <p style=""font-size:16px;line-height:1.6;margin:0 0 12px 0;"">Hola {name}.</p>
    <p style=""font-size:16px;line-height:1.6;margin:0 0 12px 0;"">Te avisamos que la contraseña de tu cuenta en Sendero se cambió hace un momento.</p>
    <p style=""font-size:14px;line-height:1.6;color:#58615C;margin:0;"">Si fuiste tú, no necesitas hacer nada. Si no reconoces este cambio, solicita un nuevo restablecimiento desde la pantalla de inicio de sesión y avisa al administrador.</p>
  </div>
</body>
</html>";

        var text =
$@"Hola {fullName}.

Te avisamos que la contraseña de tu cuenta en Sendero se cambió hace un momento.

Si fuiste tú, no necesitas hacer nada. Si no reconoces este cambio, solicita un nuevo
restablecimiento desde la pantalla de inicio de sesión y avisa al administrador.";

        return ("Tu contraseña fue modificada · Sendero", html, text);
    }
}
