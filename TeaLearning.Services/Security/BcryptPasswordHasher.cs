using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using TeaLearning.Core.Entities;

namespace TeaLearning.Services.Security;

/// <summary>
/// Implementación de IPasswordHasher para ApplicationUser utilizando el algoritmo BCrypt.
/// Sustituye la implementación predeterminada de ASP.NET Core Identity (PBKDF2 con HMAC-SHA256)
/// garantizando un hashing adaptativo y robusto frente a ataques de fuerza bruta.
/// </summary>
public class BcryptPasswordHasher : IPasswordHasher<ApplicationUser>
{
    private readonly int _workFactor;

    public BcryptPasswordHasher(IConfiguration? configuration = null)
    {
        // Se establece factor de trabajo 11 por defecto (~100-200ms por hash),
        // configurable mediante la clave "Security:BcryptWorkFactor"
        int configuredFactor = 11;
        if (configuration != null && int.TryParse(configuration["Security:BcryptWorkFactor"], out var factor) && factor >= 4 && factor <= 31)
        {
            configuredFactor = factor;
        }
        _workFactor = configuredFactor;
    }

    /// <summary>
    /// Genera un hash BCrypt para la contraseña en texto plano provista.
    /// </summary>
    public string HashPassword(ApplicationUser user, string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentNullException(nameof(password), "La contraseña no puede ser nula ni vacía.");
        }

        return BCrypt.Net.BCrypt.HashPassword(password, _workFactor);
    }

    /// <summary>
    /// Verifica si la contraseña provista coincide con el hash BCrypt almacenado.
    /// Transición limpia: si el hash previo no corresponde a un formato BCrypt válido,
    /// la verificación falla inmediatamente sin generar excepciones no controladas.
    /// </summary>
    public PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(hashedPassword) || string.IsNullOrWhiteSpace(providedPassword))
        {
            return PasswordVerificationResult.Failed;
        }

        try
        {
            bool isValid = BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword);
            return isValid ? PasswordVerificationResult.Success : PasswordVerificationResult.Failed;
        }
        catch
        {
            // Falla de manera segura ante hashes corruptos o formatos antiguos (SHA256/PBKDF2)
            return PasswordVerificationResult.Failed;
        }
    }
}
