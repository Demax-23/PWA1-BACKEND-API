using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Data.Context;
using TeaLearning.Data.Seeders;

namespace TeaLearning.Tests;

public class TestDataSeederTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:BcryptWorkFactor"] = "4"
            })
            .Build();

        var dbName = Guid.NewGuid().ToString();
        services.AddLogging();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configuration);
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));
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

        services.AddScoped<IPasswordHasher<ApplicationUser>, TeaLearning.Services.Security.BcryptPasswordHasher>();
        return services.BuildServiceProvider();
    }

    private static async Task<Dictionary<string, int>> CountsAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return new Dictionary<string, int>
        {
            ["users"] = await db.Users.CountAsync(),
            ["students"] = await db.StudentProfiles.CountAsync(),
            ["teachers"] = await db.TeacherProfiles.CountAsync(),
            ["guardians"] = await db.GuardianProfiles.CountAsync(),
            ["categories"] = await db.LearningCategories.CountAsync(),
            ["modules"] = await db.LearningModules.CountAsync(),
            ["activities"] = await db.Activities.CountAsync(),
            ["assignments"] = await db.ActivityAssignments.CountAsync(),
            ["executions"] = await db.ActivityExecutions.CountAsync(),
            ["notes"] = await db.PedagogicalNotes.CountAsync(),
            ["sensory"] = await db.SensoryPreferences.CountAsync()
        };
    }

    [Fact]
    public async Task Seeder_IsIdempotent_WhenRunTwice()
    {
        using var sp = BuildProvider();

        await DatabaseSeeder.SeedAsync(sp);
        var first = await CountsAsync(sp);

        await DatabaseSeeder.SeedAsync(sp);
        var second = await CountsAsync(sp);

        Assert.Equal(first, second);
        Assert.True(first["assignments"] > 0);
        Assert.True(first["executions"] > 0);
    }

    [Fact]
    public async Task Seeder_CreatesExpectedScenarios()
    {
        using var sp = BuildProvider();
        await DatabaseSeeder.SeedAsync(sp);

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Estudiante con >= 45 ejecuciones de escenario => 3 páginas a pageSize=20
        var s45 = await db.StudentProfiles.SingleAsync(s => s.CodigoEstudiante == "EST-TEA-007");
        var scenarioExecs = await db.ActivityExecutions
            .Where(e => e.StudentProfileId == s45.Id && e.Assignment.Activity.Titulo.StartsWith("[QA] "))
            .CountAsync();
        Assert.True(scenarioExecs >= 45);

        // Notas del estudiante de paginación: 26 activas, 4 ocultas para el acudiente, 1 inactiva
        var mateo = await db.StudentProfiles.SingleAsync(s => s.CodigoEstudiante == "EST-TEA-001");
        var qaNotes = db.PedagogicalNotes.Where(n => n.StudentProfileId == mateo.Id && n.Titulo.StartsWith("[QA] "));
        Assert.Equal(26, await qaNotes.CountAsync(n => n.IsActive));
        Assert.Equal(4, await qaNotes.CountAsync(n => n.IsActive && !n.VisibleAcudiente));
        Assert.Equal(1, await qaNotes.CountAsync(n => !n.IsActive));

        // Todos los tipos y áreas permitidos aparecen al menos una vez
        Assert.Equal(9, await qaNotes.Select(n => n.Tipo).Distinct().CountAsync());
        Assert.Equal(9, await qaNotes.Select(n => n.Area).Distinct().CountAsync());

        // Estudiante sin ejecuciones, con 2 asignaciones de inicio futuro y una inactiva
        var sNone = await db.StudentProfiles.SingleAsync(s => s.CodigoEstudiante == "EST-TEA-003");
        Assert.Equal(0, await db.ActivityExecutions.CountAsync(e => e.StudentProfileId == sNone.Id));
        Assert.Equal(2, await db.ActivityAssignments.CountAsync(a => a.StudentProfileId == sNone.Id && a.FechaInicio > DateTime.UtcNow));
        Assert.Equal(1, await db.ActivityAssignments.CountAsync(a => a.StudentProfileId == sNone.Id && !a.IsActive));
        Assert.Equal(1, await db.ActivityAssignments.CountAsync(a => a.StudentProfileId == sNone.Id && a.FechaLimite == null));

        // Cuentas en estados especiales
        var inactiveStudent = await userManager.FindByEmailAsync("estudiante13@tealearning.com");
        Assert.NotNull(inactiveStudent);
        Assert.False(inactiveStudent!.IsActive);
        Assert.False((await db.StudentProfiles.SingleAsync(s => s.CodigoEstudiante == "EST-TEA-013")).IsActive);

        var noProfile = await userManager.FindByEmailAsync("estudiante15@tealearning.com");
        Assert.NotNull(noProfile);
        Assert.False(await db.StudentProfiles.AnyAsync(s => s.UserId == noProfile!.Id));
        Assert.True(noProfile!.Nombres.Length <= 100 && noProfile.Apellidos.Length <= 100);

        var inactiveTeacher = await userManager.FindByEmailAsync("tealearning.rol+docente3@gmail.com");
        Assert.NotNull(inactiveTeacher);
        Assert.False(inactiveTeacher!.IsActive);

        // El acudiente inactivo nunca queda sin hijos
        var guardian3 = await userManager.FindByEmailAsync("tealearning.rol+acudiente3@gmail.com");
        Assert.NotNull(guardian3);
        Assert.False(guardian3!.IsActive);
        var guardian3Profile = await db.GuardianProfiles.SingleAsync(g => g.UserId == guardian3.Id);
        Assert.True(await db.StudentProfiles.AnyAsync(s => s.GuardianProfileId == guardian3Profile.Id));

        // Catálogo: módulo vacío, módulo inactivo, categoría sin módulos
        var emptyModule = await db.LearningModules.SingleAsync(m => m.Titulo == "Módulo QA: Arte con Colores (vacío)");
        Assert.Equal(0, await db.Activities.CountAsync(a => a.ModuloId == emptyModule.Id));
        Assert.False((await db.LearningModules.SingleAsync(m => m.Titulo == "Módulo QA: Música Suave (inactivo)")).IsActive);
        var artCategory = await db.LearningCategories.SingleAsync(c => c.Nombre == "Arte y expresión");
        Assert.True(artCategory.IsActive);
        Assert.Equal(0, await db.LearningModules.CountAsync(m => m.CategoriaId == artCategory.Id));

        // Módulo completo: 3 actividades
        var fullModule = await db.LearningModules.SingleAsync(m => m.Titulo == "Módulo QA: Rutina de Higiene");
        Assert.Equal(3, await db.Activities.CountAsync(a => a.ModuloId == fullModule.Id && a.IsActive));

        // Preferencias sensoriales: ya no son todas iguales
        var distinctProfiles = await db.SensoryPreferences
            .Select(p => new { p.PaletaColor, p.Tipografia, p.ModoContraste, p.NivelEscalaFuente })
            .Distinct()
            .CountAsync();
        Assert.True(distinctProfiles >= 6);
        Assert.Contains(ModoContraste.Bajo, db.SensoryPreferences.Select(p => p.ModoContraste).ToList());
        Assert.Contains(ModoContraste.AltoSuave, db.SensoryPreferences.Select(p => p.ModoContraste).ToList());
    }

    [Fact]
    public async Task Seeder_RespectsBackendInvariants()
    {
        using var sp = BuildProvider();
        await DatabaseSeeder.SeedAsync(sp);

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Regla del backend: completada exitosamente => al menos 1 acierto
        Assert.False(await db.ActivityExecutions.AnyAsync(e => e.CompletadaExitosamente && e.CantidadAciertos < 1));

        // Fechas de ejecución nunca en el futuro
        Assert.False(await db.ActivityExecutions.AnyAsync(e => e.FechaRegistro > DateTime.UtcNow.AddMinutes(1)));

        // Calificaciones en [1.0, 5.0], comentario <= 500 y calificadas con docente
        var graded = await db.ActivityAssignments.Where(a => a.Calificacion != null).ToListAsync();
        Assert.True(graded.Count >= 9);
        Assert.All(graded, a =>
        {
            Assert.InRange(a.Calificacion!.Value, 1.0m, 5.0m);
            Assert.True((a.ComentarioCalificacion?.Length ?? 0) <= 500);
            Assert.NotNull(a.CalificadoPorDocenteId);
        });
        Assert.Contains(graded, a => a.Calificacion == 1.0m);
        Assert.Contains(graded, a => a.Calificacion == 5.0m);
        Assert.Contains(graded, a => a.ComentarioCalificacion?.Length == 500);

        // Completadas sin calificar
        Assert.True(await db.ActivityAssignments.AnyAsync(a => a.Estado == EstadoAsignacion.Completada && a.Calificacion == null && a.Activity.Titulo.StartsWith("[QA] ")));

        // Ejecuciones recientes (últimos 7 días) para validar métricas "reciente"
        Assert.True(await db.ActivityExecutions.AnyAsync(e => e.FechaRegistro > DateTime.UtcNow.AddDays(-7)));

        // Notas dentro de los límites de validación del backend
        Assert.False(await db.PedagogicalNotes.AnyAsync(n => n.Titulo.Length > 100));
    }

    [Fact]
    public async Task Seeder_FixesEmptyHandWashingConfig_WithFiveValidSteps()
    {
        using var sp = BuildProvider();
        await DatabaseSeeder.SeedAsync(sp);

        Guid activityId;
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var module = await db.LearningModules.FirstAsync();
            var teacher = await db.TeacherProfiles.FirstAsync();
            var activity = new Activity
            {
                Titulo = "Lavado de Manos Paso a Paso",
                Descripcion = "Dato heredado inválido",
                TipoActividad = TipoActividad.SecuenciaLogica,
                ObjetivoPedagogico = "Higiene",
                ConfigJson = "{\"steps\":[],\"pasos\":[]}",
                ModuloId = module.Id,
                DocenteId = teacher.Id
            };
            db.Activities.Add(activity);
            await db.SaveChangesAsync();
            activityId = activity.Id;
        }

        await DatabaseSeeder.SeedAsync(sp);

        using var verifyScope = sp.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var fixedActivity = await verifyDb.Activities.SingleAsync(a => a.Id == activityId);

        using var doc = System.Text.Json.JsonDocument.Parse(fixedActivity.ConfigJson);
        Assert.Equal(5, doc.RootElement.GetProperty("pasos").GetArrayLength());
        Assert.Equal(5, doc.RootElement.GetProperty("steps").GetArrayLength());
    }
    [Fact]
    public async Task Seeder_WhenDatabaseHasFirstDeploymentData_InsertsNewScenariosIdempotently()
    {
        using var sp = BuildProvider();

        // 1. Simular la base de datos de Azure tras el primer despliegue
        using (var initScope = sp.CreateScope())
        {
            var db = initScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = initScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = initScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

            foreach (var r in new[] { "Administrador", "Docente", "Estudiante", "Acudiente" })
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(r));
            }

            // Usuarios originales con correos @tealearning.com
            var legacyAdmin = new ApplicationUser
            {
                UserName = "admin@tealearning.com",
                Email = "admin@tealearning.com",
                EmailConfirmed = true,
                Nombres = "Administrador",
                Apellidos = "Sistema",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-60)
            };
            await userManager.CreateAsync(legacyAdmin, "Admin123!*");
            await userManager.AddToRoleAsync(legacyAdmin, "Administrador");

            var legacyTeacher = new ApplicationUser
            {
                UserName = "docente@tealearning.com",
                Email = "docente@tealearning.com",
                EmailConfirmed = true,
                Nombres = "Laura",
                Apellidos = "Gómez",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-60)
            };
            await userManager.CreateAsync(legacyTeacher, "Docente123!*");
            await userManager.AddToRoleAsync(legacyTeacher, "Docente");

            var teacherProfile = new TeacherProfile
            {
                UserId = legacyTeacher.Id,
                Especialidad = "Educación Especial y TEA Nivel 1",
                NumeroRegistroProfesional = "REG-DOC-2026-01"
            };
            db.TeacherProfiles.Add(teacherProfile);

            var classroom = new Classroom
            {
                Nombre = "Aula Azul - Estimulación Cognitiva",
                Grado = "Primaria Inicial",
                AnioLectivo = DateTime.UtcNow.Year,
                TeacherProfileId = teacherProfile.Id
            };
            db.Classrooms.Add(classroom);

            var legacyGuardian = new ApplicationUser
            {
                UserName = "acudiente@tealearning.com",
                Email = "acudiente@tealearning.com",
                EmailConfirmed = true,
                Nombres = "Carlos",
                Apellidos = "Pérez",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-60)
            };
            await userManager.CreateAsync(legacyGuardian, "Acudiente123!*");
            await userManager.AddToRoleAsync(legacyGuardian, "Acudiente");

            var guardianProfile = new GuardianProfile
            {
                UserId = legacyGuardian.Id,
                TelefonoContacto = "+57 300 123 4567",
                Parentesco = "Padre",
                Direccion = "Calle 100 # 15-20"
            };
            db.GuardianProfiles.Add(guardianProfile);

            var legacyStudent = new ApplicationUser
            {
                UserName = "estudiante@tealearning.com",
                Email = "estudiante@tealearning.com",
                EmailConfirmed = true,
                Nombres = "Mateo",
                Apellidos = "Pérez",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-60)
            };
            await userManager.CreateAsync(legacyStudent, "Estudiante123!*");
            await userManager.AddToRoleAsync(legacyStudent, "Estudiante");

            var studentProfile = new StudentProfile
            {
                UserId = legacyStudent.Id,
                CodigoEstudiante = "EST-TEA-001",
                FechaNacimiento = new DateTime(2017, 5, 15),
                ObservacionesMedicasTEA = "Diagnóstico TEA Nivel 1.",
                ClassroomId = classroom.Id,
                GuardianProfileId = guardianProfile.Id
            };
            db.StudentProfiles.Add(studentProfile);

            var sensoryPref = new SensoryPreference
            {
                StudentProfileId = studentProfile.Id,
                PaletaColor = "PastelSuave",
                Tipografia = "Atkinson Hyperlegible",
                MayorEspacioEntreLetras = false,
                AudioActivo = true,
                VolumenAudio = 60,
                TextoAVoz = true,
                AnimacionesReducidas = true,
                NivelEscalaFuente = 2,
                ModoContraste = ModoContraste.Estandar,
                OcultarTemporizadores = true,
                ModoTemporizador = "sin-temporizador",
                SegundosAvisoPrevio = 15
            };
            db.SensoryPreferences.Add(sensoryPref);

            var catCom = new LearningCategory
            {
                Nombre = "Comunicación",
                Descripcion = "Habilidades de comunicación.",
                Color = "#2F6E5C",
                Orden = 1,
                IsActive = true
            };
            db.LearningCategories.Add(catCom);

            var mod1 = new LearningModule
            {
                Titulo = "Módulo 1: Identificación y Gestión Emocional",
                Descripcion = "Emociones básicas.",
                Icono = "smile",
                Orden = 1,
                CategoriaId = catCom.Id
            };
            db.LearningModules.Add(mod1);

            var act1 = new Activity
            {
                Titulo = "Emparejamiento de Emociones",
                Descripcion = "Asocia rostros.",
                TipoActividad = TipoActividad.EmparejamientoVisual,
                ObjetivoPedagogico = "Discriminación visual.",
                ConfigJson = "{\"pares\":[]}",
                ModuloId = mod1.Id,
                DocenteId = teacherProfile.Id
            };
            var act2 = new Activity
            {
                Titulo = "Secuencia de la Mañana Escolar",
                Descripcion = "Pasos de la mañana.",
                TipoActividad = TipoActividad.SecuenciaLogica,
                ObjetivoPedagogico = "Estructura temporal.",
                ConfigJson = "{\"pasos\":[\"Llegar\",\"Saludar\"]}",
                ModuloId = mod1.Id,
                DocenteId = teacherProfile.Id
            };
            var actHand = new Activity
            {
                Titulo = "Lavado de Manos Paso a Paso",
                Descripcion = "Actividad creada manualmente en primer despliegue con config vacía.",
                TipoActividad = TipoActividad.SecuenciaLogica,
                ObjetivoPedagogico = "Higiene",
                ConfigJson = "{}",
                ModuloId = mod1.Id,
                DocenteId = teacherProfile.Id
            };
            db.Activities.AddRange(act1, act2, actHand);

            var assign1 = new ActivityAssignment
            {
                ActivityId = act1.Id,
                StudentProfileId = studentProfile.Id,
                FechaInicio = DateTime.UtcNow.Date.AddDays(-10),
                FechaLimite = DateTime.UtcNow.Date.AddDays(7),
                OrdenSecuencial = 1,
                Prioridad = PrioridadActividad.Alta,
                Estado = EstadoAsignacion.Completada
            };
            db.ActivityAssignments.Add(assign1);

            var exec1 = new ActivityExecution
            {
                AssignmentId = assign1.Id,
                StudentProfileId = studentProfile.Id,
                TiempoRespuestaSegundos = 42,
                CantidadAciertos = 5,
                CantidadIntentosFallidos = 1,
                CompletadaExitosamente = true,
                FechaRegistro = DateTime.UtcNow.AddDays(-2)
            };
            db.ActivityExecutions.Add(exec1);

            var note1 = new PedagogicalNote
            {
                StudentProfileId = studentProfile.Id,
                TeacherProfileId = teacherProfile.Id,
                Titulo = "Nota inicial de Mateo",
                Tipo = "Avance",
                Area = "Socioemocional",
                Contenido = "Mateo demostró un excelente avance inicial.",
                FechaRegistro = DateTime.UtcNow.AddDays(-1),
                VisibleAcudiente = true,
                IsActive = true
            };
            db.PedagogicalNotes.Add(note1);

            await db.SaveChangesAsync();
        }

        // 2. Ejecutar DatabaseSeeder.SeedAsync contra esta base de datos preexistente
        await DatabaseSeeder.SeedAsync(sp);
        var countsAfterFirstRun = await CountsAsync(sp);

        // Validaciones: los datos nuevos deben existir correctamente
        using (var verifyScope = sp.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // Los correos antiguos se migraron a alias de Gmail
            Assert.NotNull(await userManager.FindByEmailAsync("tealearning.rol+administrador@gmail.com"));
            Assert.NotNull(await userManager.FindByEmailAsync("tealearning.rol+docente@gmail.com"));
            Assert.NotNull(await userManager.FindByEmailAsync("tealearning.rol+acudiente@gmail.com"));

            // Se crearon los 10 estudiantes de prueba (estudiante3 a estudiante12)
            for (int i = 3; i <= 12; i++)
            {
                var s = await userManager.FindByEmailAsync($"estudiante{i}@tealearning.com");
                Assert.NotNull(s);
                Assert.True(await db.StudentProfiles.AnyAsync(sp => sp.UserId == s!.Id));
            }

            // Se crearon las cuentas especiales (docente3, acudiente3, estudiante13, estudiante14, estudiante15)
            Assert.NotNull(await userManager.FindByEmailAsync("tealearning.rol+docente3@gmail.com"));
            Assert.NotNull(await userManager.FindByEmailAsync("tealearning.rol+acudiente3@gmail.com"));
            Assert.NotNull(await userManager.FindByEmailAsync("estudiante13@tealearning.com"));
            Assert.NotNull(await userManager.FindByEmailAsync("estudiante14@tealearning.com"));
            Assert.NotNull(await userManager.FindByEmailAsync("estudiante15@tealearning.com"));

            // Lavado de manos fue corregido
            var handWashing = await db.Activities.SingleAsync(a => a.Titulo == "Lavado de Manos Paso a Paso");
            using var doc = System.Text.Json.JsonDocument.Parse(handWashing.ConfigJson);
            Assert.Equal(5, doc.RootElement.GetProperty("pasos").GetArrayLength());

            // Estudiante 7 tiene >= 45 ejecuciones de escenario
            var s45 = await db.StudentProfiles.SingleAsync(s => s.CodigoEstudiante == "EST-TEA-007");
            var execCount = await db.ActivityExecutions.CountAsync(e => e.StudentProfileId == s45.Id && e.Assignment.Activity.Titulo.StartsWith("[QA] "));
            Assert.True(execCount >= 45);

            // Notas pedagógicas QA añadidas a Mateo
            var mateo = await db.StudentProfiles.SingleAsync(s => s.CodigoEstudiante == "EST-TEA-001");
            var qaNotesCount = await db.PedagogicalNotes.CountAsync(n => n.StudentProfileId == mateo.Id && n.Titulo.StartsWith("[QA] "));
            Assert.Equal(27, qaNotesCount); // 26 activas + 1 inactiva

            // El total de notas de Mateo incluye la nota inicial (1 preexistente + 27 QA = 28)
            var totalMateoNotes = await db.PedagogicalNotes.CountAsync(n => n.StudentProfileId == mateo.Id);
            Assert.Equal(28, totalMateoNotes);
        }

        // 3. Ejecutar DatabaseSeeder.SeedAsync una SEGUNDA vez para validar estricta idempotencia
        await DatabaseSeeder.SeedAsync(sp);
        var countsAfterSecondRun = await CountsAsync(sp);

        // Los conteos deben ser exactamente iguales entre la primera y segunda ejecución
        Assert.Equal(countsAfterFirstRun, countsAfterSecondRun);
    }
}
