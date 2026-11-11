using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TeaLearning.Core.Entities;
using TeaLearning.Services.DTOs.Auth;
using TeaLearning.Services.Interfaces;
using TeaLearning.Services.Security;
using TeaLearning.Services.Services;

namespace TeaLearning.Tests;

/// <summary>Colección sin paralelismo: estas pruebas modifican la variable de entorno global TEA_JWT_SECRET_KEY.</summary>
[CollectionDefinition("JwtEnvironment", DisableParallelization = true)]
public class JwtEnvironmentCollection { }

[Collection("JwtEnvironment")]
public class JwtServiceTests
{
    private static JwtService Create(Dictionary<string, string?> settings)
        => new(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

    private static readonly Dictionary<string, string?> ValidSettings = new()
    {
        ["Jwt:Key"] = "clave-super-secreta-de-pruebas-con-mas-de-32-caracteres-123456"
    };

    private static ApplicationUser User() => new()
    {
        Id = Guid.NewGuid(),
        Email = "u@test.com",
        Nombres = "Ana",
        Apellidos = "Pérez",
        SecurityStamp = "stamp"
    };

    [Fact]
    public void GenerateAccessToken_IncluyeClaimsDePerfilesYRoles()
    {
        var jwt = Create(ValidSettings);
        var user = User();
        var (e, d, a) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var (token, expiration) = jwt.GenerateAccessToken(user, new[] { "Docente", "Acudiente" }, e, d, a);

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("TeaLearningApi", parsed.Issuer);
        Assert.Contains("TeaLearningClients", parsed.Audiences);
        Assert.Equal(e.ToString(), parsed.Claims.First(c => c.Type == "estudianteId").Value);
        Assert.Equal(d.ToString(), parsed.Claims.First(c => c.Type == "docenteId").Value);
        Assert.Equal(a.ToString(), parsed.Claims.First(c => c.Type == "acudienteId").Value);
        Assert.Equal(2, parsed.Claims.Count(c => c.Type == ClaimTypes.Role || c.Type == "role"));
        Assert.Equal("stamp", parsed.Claims.First(c => c.Type == "security_stamp").Value);
        Assert.True(expiration > DateTime.UtcNow.AddMinutes(55));
    }

    [Fact]
    public void GenerateAccessToken_SinPerfilesNiConfiguracionOpcional_UsaValoresPorDefecto()
    {
        var jwt = Create(ValidSettings);
        var user = User();
        user.SecurityStamp = null;
        user.Email = null;

        var (token, _) = jwt.GenerateAccessToken(user, Array.Empty<string>());

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.DoesNotContain(parsed.Claims, c => c.Type == "estudianteId");
        Assert.DoesNotContain(parsed.Claims, c => c.Type == "docenteId");
        Assert.DoesNotContain(parsed.Claims, c => c.Type == "acudienteId");
    }

    [Fact]
    public void GenerateAccessToken_SinClave_LanzaInvalidOperation()
    {
        var jwt = Create(new Dictionary<string, string?>());
        var saved = Environment.GetEnvironmentVariable("TEA_JWT_SECRET_KEY");
        Environment.SetEnvironmentVariable("TEA_JWT_SECRET_KEY", null);
        try
        {
            Assert.Throws<InvalidOperationException>(() => jwt.GenerateAccessToken(User(), Array.Empty<string>()));
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEA_JWT_SECRET_KEY", saved);
        }
    }

    [Fact]
    public void JwtSigningKey_UsaSecretKeyComoRespaldoYVariableDeEntornoConPrioridad()
    {
        var saved = Environment.GetEnvironmentVariable("TEA_JWT_SECRET_KEY");
        try
        {
            Environment.SetEnvironmentVariable("TEA_JWT_SECRET_KEY", null);
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "respaldo-secreto-con-longitud-suficiente-para-hmac-sha256"
            }).Build();
            var fromSecretKey = JwtSigningKey.Create(cfg.GetSection("Jwt"));
            Assert.NotNull(fromSecretKey);

            Environment.SetEnvironmentVariable("TEA_JWT_SECRET_KEY", "variable-de-entorno-con-longitud-suficiente-para-hmac-256");
            var fromEnv = JwtSigningKey.Create(cfg.GetSection("Jwt"));
            Assert.NotEqual(fromSecretKey.Key, fromEnv.Key);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEA_JWT_SECRET_KEY", saved);
        }
    }

    [Fact]
    public void GenerateRefreshToken_ProduceValoresAleatoriosBase64()
    {
        var jwt = Create(ValidSettings);
        var a = jwt.GenerateRefreshToken();
        var b = jwt.GenerateRefreshToken();

        Assert.NotEqual(a, b);
        Assert.Equal(64, Convert.FromBase64String(a).Length);
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_TokenValido_DevuelveClaims()
    {
        var jwt = Create(ValidSettings);
        var user = User();
        var (token, _) = jwt.GenerateAccessToken(user, new[] { "Docente" });

        var principal = jwt.GetPrincipalFromExpiredToken(token);

        Assert.NotNull(principal);
        Assert.NotNull(principal!.FindFirst(ClaimTypes.NameIdentifier) ?? principal.FindFirst("sub"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("esto-no-es-un-jwt")]
    public void GetPrincipalFromExpiredToken_TokenInvalido_DevuelveNull(string token)
    {
        var jwt = Create(ValidSettings);
        Assert.Null(jwt.GetPrincipalFromExpiredToken(token));
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_FirmaDeOtraClave_DevuelveNull()
    {
        var other = Create(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "otra-clave-completamente-distinta-con-mas-de-32-caracteres-999999"
        });
        var (token, _) = other.GenerateAccessToken(User(), Array.Empty<string>());

        Assert.Null(Create(ValidSettings).GetPrincipalFromExpiredToken(token));
    }
}

public class AuthServiceTests : IDisposable
{
    private readonly AppTestHost _host = new();
    private IAuthService Auth => _host.Get<IAuthService>();

    public void Dispose() => _host.Dispose();

    private static RegisterRequestDto Reg(string email, string rol) => new()
    {
        Email = email,
        Password = "Clave123!*",
        Nombres = "  Ana ",
        Apellidos = " Gómez  ",
        Rol = rol
    };

    [Fact]
    public async Task Register_RolInvalido_Falla()
    {
        var result = await Auth.RegisterAsync(Reg("x@test.com", "Superhéroe"));

        Assert.False(result.Success);
        Assert.Contains("no es válido", result.Message);
    }

    [Fact]
    public async Task Register_CorreoDuplicado_Falla()
    {
        await Auth.RegisterAsync(Reg("dup@test.com", "Docente"));

        var result = await Auth.RegisterAsync(Reg("dup@test.com", "Docente"));

        Assert.False(result.Success);
        Assert.Contains("Ya existe", result.Message);
    }

    [Fact]
    public async Task Register_ContrasenaDebil_DevuelveErroresDeIdentity()
    {
        var dto = Reg("debil@test.com", "Docente");
        dto.Password = "abc";

        var result = await Auth.RegisterAsync(dto);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task Register_Docente_CreaPerfilYDevuelveTokens()
    {
        var dto = Reg("docente@test.com", "docente");
        dto.Especialidad = "Lenguaje";

        var result = await Auth.RegisterAsync(dto);

        Assert.True(result.Success);
        Assert.NotNull(result.Data!.User.DocenteId);
        Assert.Equal("Ana", result.Data.User.Nombres);
        Assert.Equal("Gómez", result.Data.User.Apellidos);
        Assert.False(string.IsNullOrEmpty(result.Data.Token));
        var profile = await _host.Context.TeacherProfiles.SingleAsync();
        Assert.Equal("Lenguaje", profile.Especialidad);
    }

    [Fact]
    public async Task Register_DocenteSinEspecialidad_UsaPorDefecto()
    {
        var result = await Auth.RegisterAsync(Reg("doc2@test.com", "Docente"));

        Assert.True(result.Success);
        Assert.Equal("Pedagogía y Educación Especial", (await _host.Context.TeacherProfiles.SingleAsync()).Especialidad);
    }

    [Fact]
    public async Task Register_Estudiante_SinAula_AsignaAulaActivaPorDefectoYPreferencias()
    {
        var classroom = await _host.CreateClassroomAsync();

        var result = await Auth.RegisterAsync(Reg("est@test.com", "Estudiante"));

        Assert.True(result.Success);
        var profile = await _host.Context.StudentProfiles.SingleAsync();
        Assert.Equal(classroom.Id, profile.ClassroomId);
        Assert.StartsWith("EST-", profile.CodigoEstudiante);
        Assert.Single(_host.Context.SensoryPreferences);
        Assert.Equal(profile.Id, result.Data!.User.EstudianteId);
    }

    [Fact]
    public async Task Register_Estudiante_ConDatosExplicitos_LosRespeta()
    {
        var (_, guardian) = await _host.CreateGuardianAsync();
        var classroom = await _host.CreateClassroomAsync();
        var dto = Reg("est2@test.com", "Estudiante");
        dto.CodigoEstudiante = "EST-XYZ";
        dto.FechaNacimiento = new DateTime(2015, 1, 2);
        dto.ClassroomId = classroom.Id;
        dto.GuardianProfileId = guardian.Id;

        var result = await Auth.RegisterAsync(dto);

        Assert.True(result.Success);
        var profile = await _host.Context.StudentProfiles.SingleAsync();
        Assert.Equal("EST-XYZ", profile.CodigoEstudiante);
        Assert.Equal(new DateTime(2015, 1, 2), profile.FechaNacimiento);
        Assert.Equal(guardian.Id, profile.GuardianProfileId);
    }

    [Fact]
    public async Task Register_Acudiente_VinculaEstudiantesPorIdOPorUsuario()
    {
        var (u1, s1) = await _host.CreateStudentAsync();
        var (_, s2) = await _host.CreateStudentAsync();
        var dto = Reg("acu@test.com", "Acudiente");
        dto.Telefono = "123";
        dto.Parentesco = "Tío";
        dto.StudentIds = new List<Guid> { s1.Id, s2.UserId };

        var result = await Auth.RegisterAsync(dto);

        Assert.True(result.Success);
        var guardianId = result.Data!.User.AcudienteId!.Value;
        Assert.All(_host.Context.StudentProfiles.ToList(), s => Assert.Equal(guardianId, s.GuardianProfileId));
        Assert.Equal(2, result.Data.User.StudentUserIds.Count);
        Assert.Equal(2, result.Data.User.StudentNombres.Count);
        Assert.NotEqual(Guid.Empty, u1.Id);
    }

    [Fact]
    public async Task Register_AcudienteSinEstudiantes_UsaValoresPorDefecto()
    {
        var result = await Auth.RegisterAsync(Reg("acu2@test.com", "Acudiente"));

        Assert.True(result.Success);
        var guardian = await _host.Context.GuardianProfiles.SingleAsync();
        Assert.Equal("Sin especificar", guardian.TelefonoContacto);
        Assert.Equal("Familiar", guardian.Parentesco);
        Assert.Empty(result.Data!.User.StudentUserIds);
    }

    [Fact]
    public async Task Register_Administrador_NoCreaPerfiles()
    {
        var result = await Auth.RegisterAsync(Reg("adm@test.com", "Administrador"));

        Assert.True(result.Success);
        Assert.Null(result.Data!.User.DocenteId);
        Assert.Null(result.Data.User.EstudianteId);
        Assert.Null(result.Data.User.AcudienteId);
    }

    [Fact]
    public async Task Login_CredencialesCorrectas_DevuelveTokenYActualizaRefresh()
    {
        var (user, teacher) = await _host.CreateTeacherAsync("login@test.com");

        var result = await Auth.LoginAsync(new LoginRequestDto { Email = "login@test.com", Password = AppTestHost.Password });

        Assert.True(result.Success);
        Assert.Equal(teacher.Id, result.Data!.User.DocenteId);
        Assert.Contains("Docente", result.Data.User.Roles);
        var stored = await _host.UserManager.FindByIdAsync(user.Id.ToString());
        Assert.Equal(result.Data.RefreshToken, stored!.RefreshToken);
        Assert.NotNull(stored.UltimaActividadAt);
    }

    [Fact]
    public async Task Login_UsuarioInexistente_Falla()
    {
        var result = await Auth.LoginAsync(new LoginRequestDto { Email = "nadie@test.com", Password = "x" });

        Assert.False(result.Success);
        Assert.Equal("Credenciales inválidas.", result.Message);
    }

    [Fact]
    public async Task Login_CuentaInactiva_Falla()
    {
        await _host.CreateUserAsync("inactivo@test.com", "Docente", isActive: false);

        var result = await Auth.LoginAsync(new LoginRequestDto { Email = "inactivo@test.com", Password = AppTestHost.Password });

        Assert.False(result.Success);
        Assert.Contains("inactiva", result.Message);
    }

    [Fact]
    public async Task Login_ContrasenaIncorrecta_Falla()
    {
        await _host.CreateUserAsync("pw@test.com", "Docente");

        var result = await Auth.LoginAsync(new LoginRequestDto { Email = "pw@test.com", Password = "Incorrecta1!" });

        Assert.False(result.Success);
    }

    private async Task<AuthResponseDto> LoginAsync(string email)
    {
        var login = await Auth.LoginAsync(new LoginRequestDto { Email = email, Password = AppTestHost.Password });
        Assert.True(login.Success);
        return login.Data!;
    }

    [Fact]
    public async Task Refresh_FlujoCorrecto_RotaElRefreshToken()
    {
        await _host.CreateStudentAsync(email: "refresh@test.com");
        var session = await LoginAsync("refresh@test.com");

        var result = await Auth.RefreshTokenAsync(new RefreshTokenRequestDto { AccessToken = session.Token, RefreshToken = session.RefreshToken });

        Assert.True(result.Success);
        Assert.NotEqual(session.RefreshToken, result.Data!.RefreshToken);
        Assert.NotNull(result.Data.User.EstudianteId);
    }

    [Fact]
    public async Task Refresh_TokenInvalido_Falla()
    {
        var result = await Auth.RefreshTokenAsync(new RefreshTokenRequestDto { AccessToken = "basura", RefreshToken = "x" });

        Assert.False(result.Success);
        Assert.Contains("inválido", result.Message);
    }

    [Fact]
    public async Task Refresh_TokenSinIdentificadorDeUsuario_Falla()
    {
        var jwt = _host.Get<IJwtService>();
        var user = new ApplicationUser { Id = Guid.Empty, Email = "x@test.com", Nombres = "A", Apellidos = "B" };
        // Sub = Guid.Empty es parseable, así que construimos un token sin 'sub' usando un id no GUID mediante un token manual.
        var key = JwtSigningKey.Create(_host.Configuration.GetSection("Jwt"));
        var descriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim("sub", "no-es-guid") }),
            Issuer = "TeaLearningApi",
            Audience = "TeaLearningClients",
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256)
        };
        var handler = new JwtSecurityTokenHandler();
        var token = handler.WriteToken(handler.CreateToken(descriptor));

        var result = await Auth.RefreshTokenAsync(new RefreshTokenRequestDto { AccessToken = token, RefreshToken = "x" });

        Assert.False(result.Success);
        Assert.Contains("Identificador", result.Message);
        Assert.NotNull(jwt);
        Assert.NotNull(user);
    }

    [Fact]
    public async Task Refresh_UsuarioDesactivado_Falla()
    {
        var user = await _host.CreateUserAsync("des@test.com", "Docente");
        var session = await LoginAsync("des@test.com");
        user.IsActive = false;
        await _host.UserManager.UpdateAsync(user);

        var result = await Auth.RefreshTokenAsync(new RefreshTokenRequestDto { AccessToken = session.Token, RefreshToken = session.RefreshToken });

        Assert.False(result.Success);
        Assert.Contains("inactivo", result.Message);
    }

    [Fact]
    public async Task Refresh_SecurityStampCambiado_InvalidaSesion()
    {
        var user = await _host.CreateUserAsync("stamp@test.com", "Docente");
        var session = await LoginAsync("stamp@test.com");
        await _host.UserManager.UpdateSecurityStampAsync(user);

        var result = await Auth.RefreshTokenAsync(new RefreshTokenRequestDto { AccessToken = session.Token, RefreshToken = session.RefreshToken });

        Assert.False(result.Success);
        Assert.Contains("invalidada", result.Message);
    }

    [Fact]
    public async Task Refresh_RefreshTokenIncorrecto_Falla()
    {
        await _host.CreateUserAsync("bad@test.com", "Docente");
        var session = await LoginAsync("bad@test.com");

        var result = await Auth.RefreshTokenAsync(new RefreshTokenRequestDto { AccessToken = session.Token, RefreshToken = "otro" });

        Assert.False(result.Success);
        Assert.Contains("expirado", result.Message);
    }

    [Fact]
    public async Task Refresh_RefreshTokenExpirado_Falla()
    {
        var user = await _host.CreateUserAsync("exp@test.com", "Docente");
        var session = await LoginAsync("exp@test.com");
        var stored = await _host.UserManager.FindByIdAsync(user.Id.ToString());
        stored!.RefreshTokenExpiryTime = DateTime.UtcNow.AddMinutes(-1);
        await _host.UserManager.UpdateAsync(stored);

        var result = await Auth.RefreshTokenAsync(new RefreshTokenRequestDto { AccessToken = session.Token, RefreshToken = session.RefreshToken });

        Assert.False(result.Success);
    }
}
