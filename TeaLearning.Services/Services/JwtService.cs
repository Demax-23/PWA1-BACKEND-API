using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TeaLearning.Core.Entities;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Services.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime Expiration) GenerateAccessToken(
        ApplicationUser user, 
        IList<string> roles, 
        Guid? estudianteId = null,
        Guid? docenteId = null,
        Guid? acudienteId = null)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var envSecretKey = Environment.GetEnvironmentVariable("TEA_JWT_SECRET_KEY");
        var configuredJwtKey = !string.IsNullOrWhiteSpace(jwtSettings["Key"])
            ? jwtSettings["Key"]
            : jwtSettings["SecretKey"];
        var secretKey = !string.IsNullOrWhiteSpace(envSecretKey)
            ? envSecretKey
            : (!string.IsNullOrWhiteSpace(configuredJwtKey)
                ? configuredJwtKey
                : "TeaLearningUltraSecretKeyTEA2026SecureJwtToken_ChangeInProd!");
        var issuer = jwtSettings["Issuer"] ?? "TeaLearningApi";
        var audience = jwtSettings["Audience"] ?? "TeaLearningClients";
        var durationMinutes = int.TryParse(jwtSettings["DurationInMinutes"], out var minutes) ? minutes : 60;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("security_stamp", user.SecurityStamp ?? string.Empty),
            new("nombreCompleto", user.NombreCompleto),
            new("nombres", user.Nombres),
            new("apellidos", user.Apellidos)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        if (estudianteId.HasValue)
        {
            claims.Add(new Claim("estudianteId", estudianteId.Value.ToString()));
        }

        if (docenteId.HasValue)
        {
            claims.Add(new Claim("docenteId", docenteId.Value.ToString()));
        }

        if (acudienteId.HasValue)
        {
            claims.Add(new Claim("acudienteId", acudienteId.Value.ToString()));
        }

        var expiration = DateTime.UtcNow.AddMinutes(durationMinutes);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiration,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return (tokenHandler.WriteToken(token), expiration);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var jwtSettings = _configuration.GetSection("Jwt");
        var envSecretKey = Environment.GetEnvironmentVariable("TEA_JWT_SECRET_KEY");
        var configuredJwtKey = !string.IsNullOrWhiteSpace(jwtSettings["Key"])
            ? jwtSettings["Key"]
            : jwtSettings["SecretKey"];
        var secretKey = !string.IsNullOrWhiteSpace(envSecretKey)
            ? envSecretKey
            : (!string.IsNullOrWhiteSpace(configuredJwtKey)
                ? configuredJwtKey
                : "TeaLearningUltraSecretKeyTEA2026SecureJwtToken_ChangeInProd!");
        var issuer = jwtSettings["Issuer"] ?? "TeaLearningApi";
        var audience = jwtSettings["Audience"] ?? "TeaLearningClients";

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateLifetime = false // Permitir tokens expirados para verificar firma
        };

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);

            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
