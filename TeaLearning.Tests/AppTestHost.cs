using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Data.Context;
using TeaLearning.Data.Repositories;
using TeaLearning.Services.Interfaces;
using TeaLearning.Services.Security;
using TeaLearning.Services.Services;

namespace TeaLearning.Tests;

/// <summary>
/// Entorno de pruebas completo: Identity real + BCrypt, base de datos en memoria aislada por instancia,
/// todos los repositorios y servicios de negocio reales y correo/almacenamiento falsos.
/// Incluye utilidades para sembrar datos y para construir controladores con un usuario autenticado.
/// </summary>
internal sealed class AppTestHost : IDisposable
{
    public const string Password = "Clave123!*";

    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    public FakeEmailService Email { get; } = new();
    public IServiceProvider Services => _scope.ServiceProvider;
    public ApplicationDbContext Context { get; }
    public UserManager<ApplicationUser> UserManager { get; }
    public IConfiguration Configuration { get; }

    public AppTestHost(Dictionary<string, string?>? extraConfig = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Security:BcryptWorkFactor"] = "4",
            ["Frontend:BaseUrl"] = PasswordTestHost.FrontendBaseUrl + "/",
            ["PasswordReset:TokenLifespanMinutes"] = "30",
            ["Jwt:Key"] = "clave-super-secreta-de-pruebas-con-mas-de-32-caracteres-123456",
            ["Jwt:Issuer"] = "TeaLearningApi",
            ["Jwt:Audience"] = "TeaLearningClients",
            ["Jwt:DurationInMinutes"] = "60",
            ["Jwt:RefreshTokenDurationInDays"] = "7"
        };
        if (extraConfig != null)
        {
            foreach (var kv in extraConfig) settings[kv.Key] = kv.Value;
        }

        Configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var databaseName = "AppTests_" + Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Configuration);
        services.AddDataProtection();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(databaseName));
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
        services.AddScoped<IPasswordHasher<ApplicationUser>, BcryptPasswordHasher>();

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<ISensoryPreferenceRepository, SensoryPreferenceRepository>();
        services.AddScoped<IActivityAssignmentRepository, ActivityAssignmentRepository>();
        services.AddScoped<ILearningCategoryRepository, LearningCategoryRepository>();
        services.AddScoped<ILearningModuleRepository, LearningModuleRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<IActivityExecutionRepository, ActivityExecutionRepository>();
        services.AddScoped<IPedagogicalNoteRepository, PedagogicalNoteRepository>();

        services.AddSingleton<IEmailService>(Email);
        services.AddSingleton<IFileStorageService, StubFileStorageService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IAuthService, AuthService>();
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
        services.AddScoped<IPasswordResetService, PasswordResetService>();

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        Context = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        UserManager = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    }

    public T Get<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    // ---------------------------------------------------------------- siembra de datos

    public async Task<ApplicationUser> CreateUserAsync(string email, string role, bool isActive = true, string? password = null)
    {
        var roleManager = Get<RoleManager<IdentityRole<Guid>>>();
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            Nombres = "Nombre " + role,
            Apellidos = "Apellido",
            IsActive = isActive
        };
        var result = await UserManager.CreateAsync(user, password ?? Password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        await UserManager.AddToRoleAsync(user, role);
        return user;
    }

    public async Task<(ApplicationUser User, TeacherProfile Profile)> CreateTeacherAsync(string? email = null)
    {
        var user = await CreateUserAsync(email ?? $"docente{Guid.NewGuid():N}@test.com", "Docente");
        var profile = new TeacherProfile { UserId = user.Id, Especialidad = "Pedagogía" };
        Context.TeacherProfiles.Add(profile);
        await Context.SaveChangesAsync();
        return (user, profile);
    }

    public async Task<(ApplicationUser User, GuardianProfile Profile)> CreateGuardianAsync(string? email = null)
    {
        var user = await CreateUserAsync(email ?? $"acudiente{Guid.NewGuid():N}@test.com", "Acudiente");
        var profile = new GuardianProfile { UserId = user.Id, TelefonoContacto = "300", Parentesco = "Madre" };
        Context.GuardianProfiles.Add(profile);
        await Context.SaveChangesAsync();
        return (user, profile);
    }

    public async Task<ApplicationUser> CreateAdminAsync(string? email = null)
        => await CreateUserAsync(email ?? $"admin{Guid.NewGuid():N}@test.com", "Administrador");

    public async Task<Classroom> CreateClassroomAsync(TeacherProfile? teacher = null, string name = "Aula Azul")
    {
        var classroom = new Classroom { Nombre = name, TeacherProfileId = teacher?.Id ?? Guid.Empty };
        if (teacher == null)
        {
            var (_, t) = await CreateTeacherAsync();
            classroom.TeacherProfileId = t.Id;
        }
        Context.Classrooms.Add(classroom);
        await Context.SaveChangesAsync();
        return classroom;
    }

    public async Task<(ApplicationUser User, StudentProfile Profile)> CreateStudentAsync(
        Classroom? classroom = null, GuardianProfile? guardian = null, string? email = null, bool withSensory = true)
    {
        var user = await CreateUserAsync(email ?? $"estudiante{Guid.NewGuid():N}@test.com", "Estudiante");
        var profile = new StudentProfile
        {
            UserId = user.Id,
            CodigoEstudiante = "EST-" + Guid.NewGuid().ToString("N")[..6],
            FechaNacimiento = new DateTime(2016, 3, 10),
            ClassroomId = classroom?.Id,
            GuardianProfileId = guardian?.Id
        };
        Context.StudentProfiles.Add(profile);
        await Context.SaveChangesAsync();

        if (withSensory)
        {
            Context.SensoryPreferences.Add(new SensoryPreference
            {
                StudentProfileId = profile.Id,
                PaletaColor = "PastelSuave",
                Tipografia = "Arial",
                AudioActivo = true,
                VolumenAudio = 50,
                NivelEscalaFuente = 2,
                ModoContraste = ModoContraste.Estandar,
                ModoTemporizador = "estandar",
                OcultarTemporizadores = false,
                SegundosAvisoPrevio = 10
            });
            await Context.SaveChangesAsync();
        }

        return (user, profile);
    }

    public async Task<LearningCategory> CreateCategoryAsync(string name = "Autonomía", bool active = true)
    {
        var category = new LearningCategory { Nombre = name, Descripcion = "Desc", IsActive = active };
        Context.LearningCategories.Add(category);
        await Context.SaveChangesAsync();
        return category;
    }

    public async Task<LearningModule> CreateModuleAsync(LearningCategory? category = null, string name = "Módulo 1", bool active = true)
    {
        var module = new LearningModule
        {
            Titulo = name,
            Descripcion = "Desc",
            IsActive = active,
            CategoriaId = category?.Id
        };
        Context.LearningModules.Add(module);
        await Context.SaveChangesAsync();
        return module;
    }

    public async Task<Activity> CreateActivityAsync(
        LearningModule module, TeacherProfile teacher, string title = "Actividad",
        TipoActividad tipo = TipoActividad.SecuenciaLogica, bool active = true, string? configJson = null)
    {
        var activity = new Activity
        {
            Titulo = title,
            Descripcion = "Desc",
            TipoActividad = tipo,
            ModuloId = module.Id,
            DocenteId = teacher.Id,
            IsActive = active,
            ConfigJson = configJson ?? "{\"pasos\":[{\"orden\":1,\"texto\":\"a\"},{\"orden\":2,\"texto\":\"b\"}]}"
        };
        Context.Activities.Add(activity);
        await Context.SaveChangesAsync();
        return activity;
    }

    public async Task<ActivityAssignment> CreateAssignmentAsync(
        Activity activity, StudentProfile student, EstadoAsignacion estado = EstadoAsignacion.Pendiente,
        int orden = 1, DateTime? start = null, DateTime? due = null)
    {
        var assignment = new ActivityAssignment
        {
            ActivityId = activity.Id,
            StudentProfileId = student.Id,
            Estado = estado,
            OrdenSecuencial = orden,
            FechaInicio = start ?? DateTime.UtcNow.Date.AddDays(-1),
            FechaLimite = due ?? DateTime.UtcNow.Date.AddDays(7),
            Prioridad = PrioridadActividad.Media,
            IsActive = true
        };
        Context.ActivityAssignments.Add(assignment);
        await Context.SaveChangesAsync();
        return assignment;
    }

    // ---------------------------------------------------------------- controladores

    public static ClaimsPrincipal Principal(Guid userId, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("sub", userId.ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    /// <summary>Asigna un HttpContext con el usuario indicado (o anónimo si userId es null) a un controlador.</summary>
    public static T WithUser<T>(T controller, Guid? userId, params string[] roles) where T : ControllerBase
    {
        var http = new DefaultHttpContext
        {
            User = userId.HasValue ? Principal(userId.Value, roles) : new ClaimsPrincipal(new ClaimsIdentity())
        };
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}

