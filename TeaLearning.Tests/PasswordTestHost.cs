using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TeaLearning.Core.Entities;
using TeaLearning.Data.Context;
using TeaLearning.Services.Interfaces;
using TeaLearning.Services.Security;
using TeaLearning.Services.Services;

namespace TeaLearning.Tests;

/// <summary>Correo falso que captura los mensajes en memoria; permite simular fallos SMTP sin usar la red.</summary>
internal sealed class FakeEmailService : IEmailService
{
    public record SentEmail(string ToAddress, string ToName, string Subject, string HtmlBody, string TextBody);

    public List<SentEmail> Sent { get; } = new();
    public bool ShouldFail { get; set; }

    public Task SendAsync(string toAddress, string toName, string subject, string htmlBody, string textBody, CancellationToken cancellationToken = default)
    {
        if (ShouldFail)
        {
            throw new InvalidOperationException("SMTP simulado no disponible.");
        }

        Sent.Add(new SentEmail(toAddress, toName, subject, htmlBody, textBody));
        return Task.CompletedTask;
    }
}

internal sealed class StubFileStorageService : IFileStorageService
{
    public Task<string> SaveAvatarAsync(Guid userId, Stream fileStream, string originalFileName, string contentType, CancellationToken cancellationToken = default)
        => Task.FromResult("/uploads/avatars/stub.png");

    public Task<bool> DeleteFileAsync(string relativePath, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}

/// <summary>
/// Entorno de pruebas con Identity real (política y BCrypt idénticos a producción), base de datos en memoria
/// y correo falso. Cada instancia usa una base de datos aislada.
/// </summary>
internal sealed class PasswordTestHost : IDisposable
{
    public const string FrontendBaseUrl = "http://frontend.test";

    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    public FakeEmailService Email { get; } = new();
    public UserManager<ApplicationUser> UserManager { get; }
    public ApplicationDbContext Context { get; }
    public IPasswordResetService PasswordReset { get; }
    public IUserService Users { get; }

    public PasswordTestHost()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:BcryptWorkFactor"] = "4",
                ["Frontend:BaseUrl"] = FrontendBaseUrl + "/",
                ["PasswordReset:TokenLifespanMinutes"] = "30"
            })
            .Build();

        var databaseName = "PasswordTests_" + Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDataProtection();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                // Misma política que ServiceCollectionExtensions.AddInfrastructureServices
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();
        services.AddScoped<IPasswordHasher<ApplicationUser>, BcryptPasswordHasher>();
        services.AddSingleton<IEmailService>(Email);
        services.AddSingleton<IFileStorageService, StubFileStorageService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IUserService, UserService>();

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();

        UserManager = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Context = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        PasswordReset = _scope.ServiceProvider.GetRequiredService<IPasswordResetService>();
        Users = _scope.ServiceProvider.GetRequiredService<IUserService>();
    }

    public async Task<ApplicationUser> CreateUserAsync(string email, string password, string role, bool isActive = true)
    {
        var roleManager = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            Nombres = "Nombre",
            Apellidos = role,
            IsActive = isActive,
            RefreshToken = "refresh-token-previo",
            RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7)
        };

        var result = await UserManager.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        await UserManager.AddToRoleAsync(user, role);
        return user;
    }

    /// <summary>Crea un acudiente y un estudiante vinculado a él (o sin acudiente si guardian es null).</summary>
    public async Task<(GuardianProfile Guardian, StudentProfile Student, ApplicationUser StudentUser)> CreateGuardianWithStudentAsync(
        string guardianEmail, string studentEmail, string studentPassword)
    {
        var guardianUser = await CreateUserAsync(guardianEmail, "Acudiente123!*", "Acudiente");
        var guardian = new GuardianProfile { UserId = guardianUser.Id, TelefonoContacto = "300", Parentesco = "Padre" };
        Context.GuardianProfiles.Add(guardian);

        var studentUser = await CreateUserAsync(studentEmail, studentPassword, "Estudiante");
        var student = new StudentProfile
        {
            UserId = studentUser.Id,
            CodigoEstudiante = "EST-" + Guid.NewGuid().ToString("N")[..5],
            FechaNacimiento = new DateTime(2017, 5, 15),
            GuardianProfileId = guardian.Id
        };
        Context.StudentProfiles.Add(student);
        await Context.SaveChangesAsync();

        return (guardian, student, studentUser);
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}
