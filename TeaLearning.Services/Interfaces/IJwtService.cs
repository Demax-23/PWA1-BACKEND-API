using System.Security.Claims;
using TeaLearning.Core.Entities;

namespace TeaLearning.Services.Interfaces;

public interface IJwtService
{
    (string Token, DateTime Expiration) GenerateAccessToken(
        ApplicationUser user, 
        IList<string> roles, 
        Guid? estudianteId = null,
        Guid? docenteId = null,
        Guid? acudienteId = null);

    string GenerateRefreshToken();

    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
