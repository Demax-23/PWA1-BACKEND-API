using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TeaLearning.Core.Entities;
using TeaLearning.Data.Context;
using TeaLearning.Data.Repositories;
using TeaLearning.Services.Interfaces;
using TeaLearning.Services.Security;
using TeaLearning.Services.Services;

namespace TeaLearning.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Configuración de Base de Datos SQL Server
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Server=(localdb)\\mssqllocaldb;Database=TeaLearningDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

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

        // 2.1 Reemplazo del algoritmo hash predeterminado (PBKDF2/SHA256) por BCrypt
        services.AddScoped<IPasswordHasher<ApplicationUser>, BcryptPasswordHasher>();

        // 3. Configuración de Autenticación JWT
        var jwtSettings = configuration.GetSection("Jwt");
        var secretKey = jwtSettings["SecretKey"] ?? "TeaLearningUltraSecretKeyTEA2026SecureJwtToken_ChangeInProd!";
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

        // 6. Configuración de CORS
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAllPolicy", builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
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
