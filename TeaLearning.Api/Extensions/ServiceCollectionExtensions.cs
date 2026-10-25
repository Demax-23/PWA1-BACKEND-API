using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TeaLearning.Core.Entities;
using TeaLearning.Data.Context;
using TeaLearning.Data.Repositories;
using TeaLearning.Services.Common;
using TeaLearning.Services.Interfaces;
using TeaLearning.Services.Security;
using TeaLearning.Services.Services;

namespace TeaLearning.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 0. Configuración segura de Proxy Inverso (Azure App Service / entorno local)
        // Solo confía en un salto (ForwardLimit = 1) proveniente de loopback o subredes internas de proxy,
        // impidiendo que clientes externos falsifiquen el primer valor de X-Forwarded-For.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();

            options.KnownProxies.Add(System.Net.IPAddress.Loopback);
            options.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);

            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("127.0.0.0"), 8));
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("10.0.0.0"), 8));
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("172.16.0.0"), 12));
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("192.168.0.0"), 16));
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("169.254.0.0"), 16));
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("fc00::"), 7));
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("fe80::"), 10));
        });

        // 1. Configuración de Base de Datos SQL Server / Azure SQL con resiliencia ante fallos transitorios
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Server=(localdb)\\mssqllocaldb;Database=TeaLearningDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
            }));

        // 2. Configuración de ASP.NET Core Identity con Guid
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 6;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // 2.0 Vigencia corta para los tokens de restablecimiento de contraseña (por defecto Identity usa 1 día)
        var tokenLifespanMinutes = int.TryParse(configuration["PasswordReset:TokenLifespanMinutes"], out var lifespan) && lifespan > 0
            ? lifespan
            : 30;
        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromMinutes(tokenLifespanMinutes));

        // 2.1 Reemplazo del algoritmo hash predeterminado (PBKDF2/SHA256) por BCrypt
        services.AddScoped<IPasswordHasher<ApplicationUser>, BcryptPasswordHasher>();

        // 3. Configuración de Autenticación JWT (soporta TEA_JWT_SECRET_KEY, Jwt:Key y Jwt:SecretKey)
        var jwtSettings = configuration.GetSection("Jwt");
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

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.SaveToken = true;
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddAuthorization();

        // 4. Registro de Repositorios
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<ISensoryPreferenceRepository, SensoryPreferenceRepository>();
        services.AddScoped<IActivityAssignmentRepository, ActivityAssignmentRepository>();
        services.AddScoped<ILearningCategoryRepository, LearningCategoryRepository>();
        services.AddScoped<ILearningModuleRepository, LearningModuleRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<IActivityExecutionRepository, ActivityExecutionRepository>();
        services.AddScoped<IPedagogicalNoteRepository, PedagogicalNoteRepository>();

        // 5. Registro de Servicios de Lógica de Negocio
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IStudentService, StudentService>();
        services.AddScoped<ISensoryPreferenceService, SensoryPreferenceService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IModuleService, ModuleService>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<IActivityExecutionService, ActivityExecutionService>();
        services.AddScoped<ITeacherAnalyticsService, TeacherAnalyticsService>();
        services.AddScoped<IGuardianMonitoringService, GuardianMonitoringService>();
        services.AddScoped<IPedagogicalNoteService, PedagogicalNoteService>();
        services.AddScoped<IAchievementService, AchievementService>();

        // 5.1 Correo transaccional y restablecimiento de contraseña
        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();

        // 5.2 Limitación de solicitudes por IP real ya resuelta por ForwardedHeadersMiddleware (evita spoofing de X-Forwarded-For)
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";
                var payload = TeaLearning.Services.Common.ApiResponse<object>.Fail(
                    "Demasiados intentos seguidos. Espera unos minutos antes de volver a intentarlo.");
                await context.HttpContext.Response.WriteAsJsonAsync(payload, cancellationToken);
            };

            static string ResolveClientIp(HttpContext context) =>
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            static RateLimitPartition<string> PerIp(HttpContext context, int permitLimit, int windowMinutes = 15) =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ResolveClientIp(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromMinutes(windowMinutes),
                        QueueLimit = 0
                    });

            options.AddPolicy("password-reset-request", context => PerIp(context, 5, 15));
            options.AddPolicy("password-reset-confirm", context => PerIp(context, 10, 15));
            options.AddPolicy("login", context => PerIp(context, 10, 5));
            options.AddPolicy("refresh-token", context => PerIp(context, 30, 5));
        });

        // 6. Configuración de CORS dinámica (soporta Array JSON o String separado por comas desde Azure App Service + Localhost)
        var originsList = new List<string>
        {
            "http://localhost:5500",
            "http://127.0.0.1:5500"
        };

        var frontendBaseUrl = configuration["Frontend:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(frontendBaseUrl))
        {
            originsList.Add(frontendBaseUrl.Trim().TrimEnd('/'));
        }

        var arrayOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (arrayOrigins != null && arrayOrigins.Length > 0)
        {
            foreach (var o in arrayOrigins)
            {
                if (!string.IsNullOrWhiteSpace(o))
                {
                    foreach (var splitOrigin in o.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        originsList.Add(splitOrigin.TrimEnd('/'));
                    }
                }
            }
        }

        var rawOriginsString = configuration["Cors:AllowedOrigins"];
        if (!string.IsNullOrWhiteSpace(rawOriginsString))
        {
            foreach (var splitOrigin in rawOriginsString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                originsList.Add(splitOrigin.TrimEnd('/'));
            }
        }

        var finalAllowedOrigins = originsList
            .Where(o => !string.IsNullOrWhiteSpace(o) && !o.Contains('<') && !o.Contains('>'))
            .Select(o => o.TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        services.AddCors(options =>
        {
            options.AddPolicy("FrontendPolicy", builder =>
            {
                builder.WithOrigins(finalAllowedOrigins)
                       .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                       .WithHeaders("Authorization", "Content-Type", "Accept", "Origin", "X-Requested-With")
                       .AllowCredentials();
            });

            options.AddPolicy("AllowAllPolicy", builder =>
            {
                builder.WithOrigins(finalAllowedOrigins)
                       .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                       .WithHeaders("Authorization", "Content-Type", "Accept", "Origin", "X-Requested-With")
                       .AllowCredentials();
            });
        });

        // 7. Configuración de Swagger con soporte Bearer Token
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "TeaLearning API - Plataforma TEA Nivel 1",
                Version = "v1",
                Description = "Backend y API RESTful especializada para el apoyo pedagógico y sensorial de estudiantes diagnosticados con TEA Nivel 1."
            });

            // Configurar soporte para Bearer Token en Swagger UI
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "Autenticación JWT usando el esquema Bearer. Ingrese 'Bearer' [espacio] y luego su token en el campo de texto.\r\n\r\nEjemplo: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        },
                        Scheme = "oauth2",
                        Name = "Bearer",
                        In = ParameterLocation.Header
                    },
                    new List<string>()
                }
            });
        });

        return services;
    }
}
