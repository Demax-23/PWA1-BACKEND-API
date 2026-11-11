using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace TeaLearning.Services.Security;

public static class JwtSigningKey
{
    public static SymmetricSecurityKey Create(IConfigurationSection jwtSettings)
    {
        var secretKey = Environment.GetEnvironmentVariable("TEA_JWT_SECRET_KEY");
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            secretKey = jwtSettings["Key"];
        }

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            secretKey = jwtSettings["SecretKey"];
        }

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("JWT Secret Key is not configured.");
        }

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
    }
}
