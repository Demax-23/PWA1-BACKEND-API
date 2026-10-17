using System.Text;

namespace TeaLearning.Services.Common;

/// <summary>
/// Codificación Base64 segura para URL (RFC 4648 §5). Los tokens de Identity contienen '+', '/' y '=',
/// que se corromperían dentro de un enlace; este helper los convierte en caracteres seguros.
/// </summary>
public static class Base64Url
{
    public static string Encode(string value)
    {
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Decodifica el valor; retorna null si el formato es inválido.</summary>
    public static string? TryDecode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        try
        {
            var base64 = value.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
