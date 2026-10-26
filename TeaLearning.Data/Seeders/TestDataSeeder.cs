using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Data.Context;

namespace TeaLearning.Data.Seeders;

/// <summary>
/// Escenarios de datos de prueba (bordes, paginación, calificaciones, insignias, visibilidad de notas,
/// preferencias sensoriales y cuentas en estados especiales) para validar la plataforma de punta a punta.
///
/// Reglas de diseño:
/// - Determinista: cada escenario usa su propio <see cref="Random"/> con semilla fija (2026 + clave estable),
///   de modo que saltarse un escenario ya sembrado no altera los valores de los demás.
/// - Idempotente: cada escenario consulta por email / título / (estudiante, actividad) antes de crear.
///   Cada paquete de asignación + calificación + ejecuciones se guarda en un único SaveChanges (atómico).
/// - Aditivo: no borra nada. Única modificación de datos existentes: corregir el ConfigJson vacío de
///   "Lavado de Manos Paso a Paso" y las preferencias sensoriales que sigan con los valores por defecto.
/// </summary>
public static class TestDataSeeder
{
    private const int BaseSeed = 2026;
    private const string QaPrefix = "[QA] ";

    // Cuentas no-estudiante: alias de Gmail (mismo esquema que las cuentas base).
    private const string InactiveTeacherEmail = "tealearning.rol+docente3@gmail.com";
    private const string InactiveGuardianEmail = "tealearning.rol+acudiente3@gmail.com";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static async Task SeedAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        Classroom classroom,
        TeacherProfile teacherProfile)
    {
        await SeedSpecialAccountsAsync(context, userManager, classroom);

        var catalog = await SeedCatalogAsync(context, teacherProfile);
        if (catalog == null) return;

        await SeedAssignmentScenariosAsync(context, classroom, teacherProfile, catalog);
        await SeedPedagogicalNotesAsync(context, teacherProfile);
        await SeedSensoryPreferencesAsync(context);
    }

    // ------------------------------------------------------------------
    // B + I. Cuentas y estudiantes en estados especiales
    // ------------------------------------------------------------------
    private static async Task SeedSpecialAccountsAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        Classroom classroom)
    {
        var now = DateTime.UtcNow;

        // Docente inactivo (login rechazado)
        var teacher3 = await EnsureUserAsync(userManager, InactiveTeacherEmail, "Docente123!*", "Docente",
            "Andrés", "Salcedo Ruiz", active: false, ultimaActividad: now.AddDays(-30));
        if (teacher3 != null && !await context.TeacherProfiles.AnyAsync(t => t.UserId == teacher3.Id))
        {
            context.TeacherProfiles.Add(new TeacherProfile
            {
                UserId = teacher3.Id,
                Especialidad = "Psicopedagogía (cuenta inactiva de prueba)",
                NumeroRegistroProfesional = "REG-DOC-2026-03",
                IsActive = false
            });
            await context.SaveChangesAsync();
        }

        // Acudiente inactivo (con el estudiante inactivo como hijo: nunca queda sin hijos)
        var guardian3 = await EnsureUserAsync(userManager, InactiveGuardianEmail, "Acudiente123!*", "Acudiente",
            "Marta", "Quintero Pardo", active: false, ultimaActividad: now);
        GuardianProfile? guardian3Profile = null;
        if (guardian3 != null)
        {
            guardian3Profile = await context.GuardianProfiles.FirstOrDefaultAsync(g => g.UserId == guardian3.Id);
            if (guardian3Profile == null)
            {
                guardian3Profile = new GuardianProfile
                {
                    UserId = guardian3.Id,
                    TelefonoContacto = "+57 300 000 0003",
                    Parentesco = "Tutor legal",
                    Direccion = "Carrera 7 # 45-10",
                    IsActive = false
                };
                context.GuardianProfiles.Add(guardian3Profile);
                await context.SaveChangesAsync();
            }
        }

        // estudiante13: usuario y perfil inactivos, asignado al aula pero NO debe aparecer en sus listados
        var student13 = await EnsureUserAsync(userManager, "estudiante13@tealearning.com", "Estudiante123!*", "Estudiante",
            "Gabriel", "Mendoza Ortiz", active: false, ultimaActividad: now.AddDays(-30));
        if (student13 != null && !await context.StudentProfiles.AnyAsync(s => s.UserId == student13.Id || s.CodigoEstudiante == "EST-TEA-013"))
        {
            context.StudentProfiles.Add(new StudentProfile
            {
                UserId = student13.Id,
                CodigoEstudiante = "EST-TEA-013",
                FechaNacimiento = new DateTime(2016, 8, 9),
                ObservacionesMedicasTEA = "Estudiante inactivo de prueba (no debe aparecer en listados activos).",
                ClassroomId = classroom.Id,
                GuardianProfileId = guardian3Profile?.Id,
                IsActive = false
            });
            await context.SaveChangesAsync();
        }

        // estudiante14: sin aula, sin acudiente, sin preferencias sensoriales y con refresh token expirado
        var student14 = await EnsureUserAsync(userManager, "estudiante14@tealearning.com", "Estudiante123!*", "Estudiante",
            "Daniela", "Cárdenas Vega", active: true, ultimaActividad: now.AddDays(-1),
            configure: u =>
            {
                u.RefreshToken = "qa-expired-refresh-token";
                u.RefreshTokenExpiryTime = now.AddDays(-3);
            });
        if (student14 != null && !await context.StudentProfiles.AnyAsync(s => s.UserId == student14.Id || s.CodigoEstudiante == "EST-TEA-014"))
        {
            context.StudentProfiles.Add(new StudentProfile
            {
                UserId = student14.Id,
                CodigoEstudiante = "EST-TEA-014",
                FechaNacimiento = new DateTime(2017, 2, 21),
                ObservacionesMedicasTEA = "Sin aula ni acudiente asignados (estado vacío de prueba).",
                ClassroomId = null,
                GuardianProfileId = null
            });
            await context.SaveChangesAsync();
        }

        // estudiante15: usuario con rol Estudiante SIN perfil asociado; nombres largos con tildes y ñ; sin actividad previa
        await EnsureUserAsync(userManager, "estudiante15@tealearning.com", "Estudiante123!*", "Estudiante",
            "María de los Ángeles Guadalupe Concepción del Pilar",
            "Núñez de la Peña y Fernández de Córdoba Castañeda Muñoz",
            active: true, ultimaActividad: null);
    }

    private static async Task<ApplicationUser?> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string role,
        string nombres,
        string apellidos,
        bool active,
        DateTime? ultimaActividad,
        Action<ApplicationUser>? configure = null)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing != null) return existing;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            Nombres = nombres,
            Apellidos = apellidos,
            IsActive = active,
            CreatedAt = DateTime.UtcNow,
            UltimaActividadAt = ultimaActividad
        };
        configure?.Invoke(user);

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded) return null;

        await userManager.AddToRoleAsync(user, role);
        return user;
    }

    // ------------------------------------------------------------------
    // C. Catálogo: categorías, módulos y actividades
    // ------------------------------------------------------------------
    private sealed class Catalog
    {
        public Activity Cepillado { get; init; } = null!;
        public Activity Vestirse { get; init; } = null!;
        public Activity SaludarMinimo { get; init; } = null!;
        public Activity Mochila { get; init; } = null!;
        public Activity Animales { get; init; } = null!;
        public Activity Emociones { get; init; } = null!;
        public Activity Turnos { get; init; } = null!;
        public Activity SaludoGrupal { get; init; } = null!;
        public Activity Inactiva { get; init; } = null!;
    }

    private static async Task<Catalog?> SeedCatalogAsync(ApplicationDbContext context, TeacherProfile teacher)
    {
        // Categorías
        var catAutonomia = await EnsureCategoryAsync(context, "Autonomía personal",
            "Higiene, vestido y organización de pertenencias.", "#7A5C99", 4, true);
        await EnsureCategoryAsync(context, "Arte y expresión",
            "Expresión creativa con colores, música y movimiento (sin módulos aún).", "#B5542B", 5, true);

        // Módulos: completo (3 actividades), vacío e inactivo (bajo categoría activa)
        var modHigiene = await EnsureModuleAsync(context, "Módulo QA: Rutina de Higiene",
            "Módulo de prueba completo: 3 actividades de autonomía.", "droplet", 10, catAutonomia.Id, true);
        await EnsureModuleAsync(context, "Módulo QA: Arte con Colores (vacío)",
            "Módulo de prueba sin actividades.", "palette", 11, catAutonomia.Id, true);
        await EnsureModuleAsync(context, "Módulo QA: Música Suave (inactivo)",
            "Módulo de prueba inactivo bajo una categoría activa.", "music", 12, catAutonomia.Id, false);

        // Módulo general existente para actividades que no pertenecen al módulo de higiene
        var generalModule = await context.LearningModules
            .Where(m => m.IsActive && m.Id != modHigiene.Id)
            .OrderBy(m => m.Titulo.StartsWith("Módulo 2") ? 0 : 1)
            .ThenBy(m => m.Orden)
            .FirstOrDefaultAsync() ?? modHigiene;

        // Corregir "Lavado de Manos Paso a Paso" solo si su ConfigJson sigue vacío/ inválido
        await FixHandWashingConfigAsync(context);

        var catalog = new Catalog
        {
            Cepillado = await EnsureActivityAsync(context, teacher, modHigiene.Id, "Cepillado de dientes",
                TipoActividad.SecuenciaLogica, "Afianzar la rutina de higiene bucal.",
                StepsConfig("Toma tu cepillo y la crema dental", "Pon un poquito de crema en el cepillo",
                    "Cepilla arriba, abajo y por los lados", "Enjuágate la boca con agua", "Guarda tu cepillo en su lugar")),
            Vestirse = await EnsureActivityAsync(context, teacher, modHigiene.Id, "Vestirse paso a paso",
                TipoActividad.SecuenciaLogica, "Fomentar la autonomía al vestirse.",
                StepsConfig("Elige la ropa del día", "Ponte la camiseta", "Ponte el pantalón", "Ponte las medias",
                    "Ponte los zapatos", "Revisa en el espejo que estés listo")),
            // Secuencia mínima válida: 2 pasos
            SaludarMinimo = await EnsureActivityAsync(context, teacher, modHigiene.Id, "Saludar (secuencia mínima)",
                TipoActividad.SecuenciaLogica, "Caso límite: secuencia con el mínimo de 2 pasos.",
                StepsConfig("Mira a la persona", "Di hola con una sonrisa")),
            // Secuencia larga: 12 pasos
            Mochila = await EnsureActivityAsync(context, teacher, generalModule.Id, "Mi mochila completa (12 pasos)",
                TipoActividad.SecuenciaLogica, "Caso límite: secuencia larga de 12 pasos.",
                StepsConfig("Abre tu mochila", "Saca lo que no necesitas", "Pon el cuaderno de matemáticas",
                    "Pon el cuaderno de lenguaje", "Pon el estuche con lápices", "Pon la botella de agua",
                    "Pon la lonchera", "Revisa el horario", "Pon el libro de lectura", "Revisa que no falte nada",
                    "Cierra el cierre", "Cuelga la mochila en la puerta")),
            Animales = await EnsureActivityAsync(context, teacher, generalModule.Id, "Animales y sus sonidos",
                TipoActividad.EmparejamientoVisual, "Asociar cada animal con el sonido que hace.",
                JsonSerializer.Serialize(new
                {
                    parejas = new[]
                    {
                        new { leftLabel = "Perro", rightLabel = "Guau" },
                        new { leftLabel = "Gato", rightLabel = "Miau" },
                        new { leftLabel = "Vaca", rightLabel = "Muu" },
                        new { leftLabel = "Pato", rightLabel = "Cuac" }
                    }
                }, JsonOptions)),
            Emociones = await EnsureActivityAsync(context, teacher, generalModule.Id, "Reconociendo cómo se sienten",
                TipoActividad.AsociacionEmocional, "Relacionar situaciones cotidianas con emociones.",
                JsonSerializer.Serialize(new
                {
                    emociones = new[]
                    {
                        new { nombre = "Alegría", situacion = "Recibes un abrazo de tu familia" },
                        new { nombre = "Tristeza", situacion = "Se rompe tu juguete favorito" },
                        new { nombre = "Enojo", situacion = "Alguien toma tus cosas sin pedir permiso" },
                        new { nombre = "Sorpresa", situacion = "Encuentras un regalo en tu mesa" }
                    }
                }, JsonOptions)),
            Turnos = await EnsureActivityAsync(context, teacher, generalModule.Id, "Turnos en el juego",
                TipoActividad.RutinaSocial, "Practicar la espera de turnos en un juego compartido.",
                JsonSerializer.Serialize(new
                {
                    escenas = new[]
                    {
                        new { titulo = "Esperar mi turno", pasos = new[] { "Miro quién va primero", "Espero con calma", "Cuando me toca, juego" } },
                        new { titulo = "Pedir prestado", pasos = new[] { "Digo por favor", "Espero la respuesta", "Digo gracias" } }
                    }
                }, JsonOptions)),
            SaludoGrupal = await EnsureActivityAsync(context, teacher, generalModule.Id, "Saludo grupal (asignación al aula)",
                TipoActividad.RutinaSocial, "Actividad asignada en lote a todos los estudiantes del aula.",
                JsonSerializer.Serialize(new
                {
                    escenas = new[]
                    {
                        new { titulo = "Saludar a mis compañeros", pasos = new[] { "Entro al salón", "Miro a mis compañeros", "Digo buenos días" } }
                    }
                }, JsonOptions)),
            Inactiva = await EnsureActivityAsync(context, teacher, generalModule.Id, "Actividad inactiva de prueba",
                TipoActividad.SecuenciaLogica, "Actividad desactivada (no debe ofrecerse para asignar).",
                StepsConfig("Paso uno", "Paso dos", "Paso tres"), isActive: false)
        };

        return catalog;
    }

    private static async Task<LearningCategory> EnsureCategoryAsync(
        ApplicationDbContext context, string nombre, string descripcion, string color, int orden, bool active)
    {
        var existing = await context.LearningCategories.FirstOrDefaultAsync(c => c.Nombre == nombre);
        if (existing != null) return existing;

        var category = new LearningCategory
        {
            Nombre = nombre,
            Descripcion = descripcion,
            Color = color,
            Orden = orden,
            IsActive = active
        };
        context.LearningCategories.Add(category);
        await context.SaveChangesAsync();
        return category;
    }

    private static async Task<LearningModule> EnsureModuleAsync(
        ApplicationDbContext context, string titulo, string descripcion, string icono, int orden, Guid categoriaId, bool active)
    {
        var existing = await context.LearningModules.FirstOrDefaultAsync(m => m.Titulo == titulo);
        if (existing != null) return existing;

        var module = new LearningModule
        {
            Titulo = titulo,
            Descripcion = descripcion,
            Icono = icono,
            Orden = orden,
            CategoriaId = categoriaId,
            IsActive = active
        };
        context.LearningModules.Add(module);
        await context.SaveChangesAsync();
        return module;
    }

    private static async Task<Activity> EnsureActivityAsync(
        ApplicationDbContext context, TeacherProfile teacher, Guid moduloId, string titulo, TipoActividad tipo,
        string objetivo, string configJson, bool isActive = true)
    {
        var fullTitle = QaPrefix + titulo;
        var existing = await context.Activities.FirstOrDefaultAsync(a => a.Titulo == fullTitle);
        if (existing != null) return existing;

        var activity = new Activity
        {
            Titulo = fullTitle,
            Descripcion = $"Actividad de prueba ({tipo}).",
            TipoActividad = tipo,
            ObjetivoPedagogico = objetivo,
            ConfigJson = configJson,
            ModuloId = moduloId,
            DocenteId = teacher.Id,
            IsActive = isActive
        };
        context.Activities.Add(activity);
        await context.SaveChangesAsync();
        return activity;
    }

    private static string StepsConfig(params string[] labels)
    {
        return JsonSerializer.Serialize(new
        {
            steps = labels.Select((label, i) => new { id = i + 1, order = i + 1, label, icon = "circulo" }),
            pasos = labels
        }, JsonOptions);
    }

    private static async Task FixHandWashingConfigAsync(ApplicationDbContext context)
    {
        var activity = await context.Activities.FirstOrDefaultAsync(a => a.Titulo == "Lavado de Manos Paso a Paso");
        if (activity == null) return;

        if (CountValidSteps(activity.ConfigJson) >= 2) return;

        activity.ConfigJson = StepsConfig(
            "Abre el grifo y mójate las manos",
            "Ponte jabón en las palmas",
            "Frota las manos, los dedos y las uñas",
            "Enjuaga el jabón con agua",
            "Sécate con la toalla");
        activity.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    private static int CountValidSteps(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson)) return 0;
        try
        {
            using var doc = JsonDocument.Parse(configJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return 0;

            foreach (var key in new[] { "pasos", "steps" })
            {
                if (doc.RootElement.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.Array)
                {
                    int count = 0;
                    foreach (var item in el.EnumerateArray())
                    {
                        string? label = item.ValueKind == JsonValueKind.String
                            ? item.GetString()
                            : item.ValueKind == JsonValueKind.Object && item.TryGetProperty("label", out var l) && l.ValueKind == JsonValueKind.String
                                ? l.GetString()
                                : null;
                        if (!string.IsNullOrWhiteSpace(label)) count++;
                    }
                    if (count > 0) return count;
                }
            }
            return 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    // ------------------------------------------------------------------
    // D + E + F. Asignaciones, calificaciones y ejecuciones
    // ------------------------------------------------------------------
    private sealed record ExecSpec(int DaysAgo, int Seconds, int Aciertos, int Fallos, bool Ok);

    private sealed class AssignmentSpec
    {
        public Activity Activity { get; init; } = null!;
        public DateTime FechaInicio { get; init; }
        public DateTime? FechaLimite { get; init; }
        public int Orden { get; init; } = 1;
        public PrioridadActividad Prioridad { get; init; } = PrioridadActividad.Media;
        public bool IsActive { get; init; } = true;
        public decimal? Calificacion { get; init; }
        public string? Comentario { get; init; }
        public List<ExecSpec> Execs { get; init; } = new();
    }

    private static async Task SeedAssignmentScenariosAsync(
        ApplicationDbContext context, Classroom classroom, TeacherProfile teacher, Catalog c)
    {
        var today = DateTime.UtcNow.Date;

        // --- Estudiante con TODAS las insignias, módulo completo y 45 ejecuciones (3 páginas a pageSize=20) ---
        var s45 = await FindStudentAsync(context, "EST-TEA-007");
        if (s45 != null)
        {
            var rnd = Rng("EST-TEA-007");
            var plan = new (Activity act, int n, bool complete, int latest, decimal? grade, string? comment)[]
            {
                (c.Cepillado, 8, true, 0, 5.0m, "Excelente: completó la secuencia sin ayuda."),
                (c.Vestirse, 6, true, 3, 4.5m, null),
                (c.SaludarMinimo, 5, true, 1, 1.0m, "Requiere práctica constante del contacto visual."),
                (c.Animales, 7, true, 5, null, null),      // completada SIN calificar
                (c.Emociones, 6, true, 9, 3.5m, null),
                (c.Turnos, 5, true, 14, null, null),       // completada SIN calificar
                (c.Mochila, 8, false, 2, null, null)       // en progreso: solo intentos fallidos
            };

            int orden = 1;
            foreach (var p in plan)
            {
                AddAssignment(context, teacher, s45, new AssignmentSpec
                {
                    Activity = p.act,
                    FechaInicio = today.AddDays(-56),
                    FechaLimite = today.AddDays(7),
                    Orden = orden++,
                    Prioridad = PrioridadActividad.Media,
                    Calificacion = p.grade,
                    Comentario = p.comment,
                    Execs = BuildExecs(rnd, p.n, p.complete, p.latest)
                });
            }
            await SaveIfPendingAsync(context);
        }

        // --- Estudiante SIN ejecuciones ni insignias: asignaciones con fechas y estados límite ---
        var sNone = await FindStudentAsync(context, "EST-TEA-003");
        if (sNone != null)
        {
            AddAssignment(context, teacher, sNone, new AssignmentSpec
            {   // inicio futuro (+1 d): NO debe aparecer en la rutina diaria de hoy
                Activity = c.Animales, FechaInicio = today.AddDays(1), FechaLimite = today.AddDays(8),
                Orden = 1, Prioridad = PrioridadActividad.Alta
            });
            AddAssignment(context, teacher, sNone, new AssignmentSpec
            {   // inicio futuro (+7 d)
                Activity = c.Emociones, FechaInicio = today.AddDays(7), FechaLimite = today.AddDays(14),
                Orden = 2, Prioridad = PrioridadActividad.Baja
            });
            AddAssignment(context, teacher, sNone, new AssignmentSpec
            {   // vencida y pendiente
                Activity = c.Cepillado, FechaInicio = today.AddDays(-10), FechaLimite = today.AddDays(-3),
                Orden = 1, Prioridad = PrioridadActividad.Media
            });
            AddAssignment(context, teacher, sNone, new AssignmentSpec
            {   // sin fecha límite y MISMO OrdenSecuencial que la anterior
                Activity = c.Vestirse, FechaInicio = today.AddDays(-2), FechaLimite = null,
                Orden = 1, Prioridad = PrioridadActividad.Baja
            });
            AddAssignment(context, teacher, sNone, new AssignmentSpec
            {   // asignación inactiva (borrado lógico)
                Activity = c.Inactiva, FechaInicio = today.AddDays(-5), FechaLimite = today.AddDays(5),
                Orden = 5, Prioridad = PrioridadActividad.Media, IsActive = false
            });
            await SaveIfPendingAsync(context);
        }

        // --- Telemetría extrema: reintentos, 3 600 s y 0 aciertos / 0 fallos ---
        var sEdge = await FindStudentAsync(context, "EST-TEA-009");
        if (sEdge != null)
        {
            AddAssignment(context, teacher, sEdge, new AssignmentSpec
            {
                Activity = c.Mochila, FechaInicio = today.AddDays(-14), FechaLimite = today.AddDays(3),
                Orden = 1, Prioridad = PrioridadActividad.Alta,
                Execs =
                [
                    new ExecSpec(12, 60, 0, 4, false),
                    new ExecSpec(12, 55, 0, 5, false),
                    new ExecSpec(11, 70, 1, 6, false),
                    new ExecSpec(10, 3600, 1, 1, false),   // tiempo muy largo
                    new ExecSpec(9, 5, 0, 0, false),       // 0 aciertos y 0 fallos
                    new ExecSpec(8, 48, 0, 3, false),
                    new ExecSpec(5, 52, 4, 1, true)        // éxito tras varios reintentos
                ]
            });
            await SaveIfPendingAsync(context);
        }

        // --- ~12 calificaciones (bordes 1.0 / 5.0 / 3.5, con y sin comentario, límite de 500 caracteres) ---
        var longComment = Truncate(string.Concat(Enumerable.Repeat(
            "Observación detallada del desempeño del estudiante durante la actividad. ", 8)), 500);

        (decimal? grade, string? comment)[] grades =
        [
            (1.0m, "Necesita refuerzo en la secuencia de pasos."),
            (5.0m, "Desempeño sobresaliente."),
            (3.5m, null),
            (4.2m, longComment),     // comentario de exactamente 500 caracteres
            (2.0m, null),
            (4.8m, "Excelente concentración y autonomía."),
            (null, null),            // completada SIN calificar
            (3.0m, "Avanza a su propio ritmo."),
            (null, null),            // completada SIN calificar
            (5.0m, null),
            (null, null),            // completada SIN calificar
            (2.5m, "Requiere acompañamiento visual adicional.")
        ];
        Activity[] gradedActivities = [c.Cepillado, c.Vestirse, c.SaludarMinimo, c.Animales, c.Emociones, c.Turnos];
        string[] gradedStudents = ["EST-TEA-001", "EST-TEA-004", "EST-TEA-006", "EST-TEA-008", "EST-TEA-010", "EST-TEA-011"];

        int g = 0;
        foreach (var code in gradedStudents)
        {
            var student = await FindStudentAsync(context, code);
            var rnd = Rng("grades-" + code);
            for (int k = 0; k < 2; k++, g++)
            {
                if (student == null) continue;
                var (grade, comment) = grades[g];
                AddAssignment(context, teacher, student, new AssignmentSpec
                {
                    Activity = gradedActivities[(g + k) % gradedActivities.Length],
                    FechaInicio = today.AddDays(-30),
                    FechaLimite = today.AddDays(-2),
                    Orden = 10 + k,
                    Prioridad = (PrioridadActividad)(g % 3),
                    Calificacion = grade,
                    Comentario = comment,
                    Execs = BuildExecs(rnd, rnd.Next(1, 4), true, rnd.Next(1, 20))
                });
            }
            await SaveIfPendingAsync(context);
        }

        // --- Asignación en lote a todo el aula (con ejecuciones en los últimos 7 días para ~la mitad) ---
        var classroomStudents = await context.StudentProfiles
            .Where(s => s.ClassroomId == classroom.Id && s.IsActive)
            .OrderBy(s => s.CodigoEstudiante)
            .ToListAsync();
        foreach (var student in classroomStudents)
        {
            var rnd = Rng("batch-" + student.CodigoEstudiante);
            bool didIt = rnd.Next(2) == 0;
            AddAssignment(context, teacher, student, new AssignmentSpec
            {
                Activity = c.SaludoGrupal,
                FechaInicio = today.AddDays(-3),
                FechaLimite = today.AddDays(4),
                Orden = 5,
                Prioridad = PrioridadActividad.Media,
                Execs = didIt
                    ? new List<ExecSpec> { new(rnd.Next(0, 6), rnd.Next(20, 90), rnd.Next(3, 7), rnd.Next(0, 3), true) }
                    : new List<ExecSpec>()
            });
            await SaveIfPendingAsync(context);
        }
    }

    private static void AddAssignment(
        ApplicationDbContext context, TeacherProfile teacher, StudentProfile student, AssignmentSpec spec)
    {
        // Idempotencia por (estudiante, actividad): se consulta tanto la BD como lo ya agregado al contexto
        bool inContext = context.ChangeTracker.Entries<ActivityAssignment>()
            .Any(e => e.State == EntityState.Added
                      && e.Entity.StudentProfileId == student.Id
                      && e.Entity.ActivityId == spec.Activity.Id);
        if (inContext) return;

        bool exists = context.ActivityAssignments
            .Any(a => a.StudentProfileId == student.Id && a.ActivityId == spec.Activity.Id);
        if (exists) return;

        var rnd = Rng($"dates-{student.CodigoEstudiante}-{spec.Activity.Titulo}");
        var assignment = new ActivityAssignment
        {
            ActivityId = spec.Activity.Id,
            StudentProfileId = student.Id,
            FechaInicio = spec.FechaInicio,
            FechaLimite = spec.FechaLimite,
            OrdenSecuencial = spec.Orden,
            Prioridad = spec.Prioridad,
            IsActive = spec.IsActive,
            Estado = spec.Execs.Any(e => e.Ok)
                ? EstadoAsignacion.Completada
                : spec.Execs.Count > 0 ? EstadoAsignacion.EnProgreso : EstadoAsignacion.Pendiente
        };
        context.ActivityAssignments.Add(assignment);

        DateTime lastExec = DateTime.MinValue;
        foreach (var e in spec.Execs)
        {
            var when = ExecDate(rnd, e.DaysAgo);
            if (when > lastExec) lastExec = when;

            context.ActivityExecutions.Add(new ActivityExecution
            {
                Assignment = assignment,
                StudentProfileId = student.Id,
                TiempoRespuestaSegundos = e.Seconds,
                CantidadAciertos = e.Aciertos,
                CantidadIntentosFallidos = e.Fallos,
                CompletadaExitosamente = e.Ok,
                FechaRegistro = when
            });
        }

        if (spec.Calificacion.HasValue)
        {
            assignment.Calificacion = spec.Calificacion;
            assignment.ComentarioCalificacion = spec.Comentario;
            var gradedAt = lastExec == DateTime.MinValue ? DateTime.UtcNow : lastExec.AddHours(2);
            assignment.FechaCalificacion = gradedAt > DateTime.UtcNow ? DateTime.UtcNow : gradedAt;
            assignment.CalificadoPorDocenteId = teacher.Id;
        }
    }

    private static async Task SaveIfPendingAsync(ApplicationDbContext context)
    {
        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Genera <paramref name="n"/> ejecuciones cronológicas repartidas en las últimas 8 semanas.
    /// Si <paramref name="endsInSuccess"/>, la última (más reciente, a <paramref name="latestDaysAgo"/> días)
    /// es exitosa con al menos 1 acierto (regla del backend); las anteriores son intentos fallidos.
    /// </summary>
    private static List<ExecSpec> BuildExecs(Random rnd, int n, bool endsInSuccess, int latestDaysAgo)
    {
        var days = Enumerable.Range(0, n)
            .Select(_ => rnd.Next(latestDaysAgo, 56))
            .OrderByDescending(d => d)
            .ToList();
        days[n - 1] = latestDaysAgo;

        var list = new List<ExecSpec>(n);
        for (int i = 0; i < n; i++)
        {
            bool ok = endsInSuccess && i == n - 1;
            list.Add(new ExecSpec(
                days[i],
                rnd.Next(20, 150),
                ok ? rnd.Next(3, 11) : rnd.Next(0, 4),
                ok ? rnd.Next(0, 3) : rnd.Next(2, 9),
                ok));
        }
        return list;
    }

    private static DateTime ExecDate(Random rnd, int daysAgo)
    {
        var date = DateTime.UtcNow.Date.AddDays(-daysAgo).AddHours(rnd.Next(13, 22)).AddMinutes(rnd.Next(0, 60));
        return date > DateTime.UtcNow ? DateTime.UtcNow.AddMinutes(-rnd.Next(5, 90)) : date;
    }

    // ------------------------------------------------------------------
    // G. Notas pedagógicas (bitácora): paginación, visibilidad y matriz Tipo × Área
    // ------------------------------------------------------------------
    private static async Task SeedPedagogicalNotesAsync(ApplicationDbContext context, TeacherProfile teacher)
    {
        var mateo = await FindStudentAsync(context, "EST-TEA-001")
            ?? await context.StudentProfiles.FirstOrDefaultAsync(s => s.IsActive);
        if (mateo == null) return;

        string[] tipos = ["Avance", "Recomendacion", "Observacion", "AspectoAReforzar", "Conductual", "Sensorial", "Logro", "Alerta", "General"];
        string[] areas = ["Academica", "Comunicacion", "Socioemocional", "Autonomia", "Sensorial", "Cognitiva", "Social", "Regulacion", "General"];
        string[] contenidos =
        [
            "Mateo participó con calma en la actividad y siguió la secuencia con apoyos visuales.",
            "Se observó mayor tolerancia a los cambios de actividad durante la jornada.",
            "Requirió una pausa sensorial corta antes de continuar con la tarea.",
            "Respondió con entusiasmo al refuerzo positivo y mantuvo la atención por más tiempo.",
            "Comunicó una necesidad usando una frase completa sin ayuda del adulto.",
            "Mostró dificultad al esperar su turno, se practicó la espera con temporizador visual."
        ];

        var existingTitles = (await context.PedagogicalNotes
                .Where(n => n.StudentProfileId == mateo.Id && n.Titulo.StartsWith(QaPrefix))
                .Select(n => n.Titulo)
                .ToListAsync())
            .ToHashSet();

        var mateoAssignmentId = await context.ActivityAssignments
            .Where(a => a.StudentProfileId == mateo.Id)
            .OrderBy(a => a.CreatedAt)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync();

        var today = DateTime.UtcNow.Date;
        for (int i = 1; i <= 26; i++)
        {
            var titulo = $"{QaPrefix}Seguimiento {i:00}";
            if (existingTitles.Contains(titulo)) continue;

            context.PedagogicalNotes.Add(new PedagogicalNote
            {
                StudentProfileId = mateo.Id,
                TeacherProfileId = teacher.Id,
                Titulo = titulo,
                Tipo = tipos[(i - 1) % tipos.Length],
                Area = areas[((i - 1) + (i - 1) / tipos.Length) % areas.Length],
                Contenido = contenidos[(i - 1) % contenidos.Length],
                FechaRegistro = today.AddDays(-(i * 2)).AddHours(15),
                // Cada 6.ª nota es solo para el docente (el acudiente NO debe verla)
                VisibleAcudiente = i % 6 != 0,
                RecomendacionCasa = i % 2 == 0 ? "Practicar en casa la rutina con apoyos visuales durante 10 minutos." : null,
                AssignmentId = i % 5 == 0 ? mateoAssignmentId : null,
                IsActive = true
            });
        }

        // Nota inactiva (borrado lógico): no debe aparecer para nadie
        var deletedTitle = QaPrefix + "Nota eliminada (borrado lógico)";
        if (!existingTitles.Contains(deletedTitle))
        {
            context.PedagogicalNotes.Add(new PedagogicalNote
            {
                StudentProfileId = mateo.Id,
                TeacherProfileId = teacher.Id,
                Titulo = deletedTitle,
                Tipo = "General",
                Area = "General",
                Contenido = "Esta nota fue eliminada y no debe mostrarse en ninguna vista.",
                FechaRegistro = today.AddDays(-1),
                VisibleAcudiente = true,
                IsActive = false
            });
        }

        await SaveIfPendingAsync(context);
    }

    // ------------------------------------------------------------------
    // H. Preferencias sensoriales variadas
    // ------------------------------------------------------------------
    private static async Task SeedSensoryPreferencesAsync(ApplicationDbContext context)
    {
        // codigo, paleta, tipografía, contraste, escala(1-4), audio, volumen, textoAVoz, animReducidas,
        // ocultarTemporizadores, modoTemporizador, segundosAviso, mayorEspacio
        var profiles = new (string code, string paleta, string tipo, ModoContraste contraste, int escala, bool audio, int vol,
            bool tts, bool anim, bool ocultar, string modo, int aviso, bool espacio)[]
        {
            ("EST-TEA-004", "Neutro", "Arial", ModoContraste.Bajo, 1, false, 0, true, true, true, "sin-temporizador", 15, false),
            ("EST-TEA-005", "EscalaGrises", "Verdana", ModoContraste.AltoSuave, 4, true, 100, true, false, false, "estandar", 60, false),
            ("EST-TEA-006", "PastelSuave", "OpenDyslexic", ModoContraste.Estandar, 3, true, 60, true, true, false, "tiempo-extendido", 0, true),
            ("EST-TEA-008", "Neutro", "OpenDyslexic", ModoContraste.Bajo, 4, true, 30, false, true, false, "tiempo-extendido", 30, false),
            ("EST-TEA-010", "PastelSuave", "Verdana", ModoContraste.AltoSuave, 1, false, 0, true, false, true, "sin-temporizador", 15, false),
            ("EST-TEA-011", "EscalaGrises", "Atkinson Hyperlegible", ModoContraste.Estandar, 3, true, 100, true, true, false, "estandar", 45, true)
        };

        foreach (var p in profiles)
        {
            var student = await FindStudentAsync(context, p.code);
            if (student == null) continue;

            var pref = await context.SensoryPreferences.FirstOrDefaultAsync(s => s.StudentProfileId == student.Id);
            if (pref == null) continue;

            // Solo se pisan las preferencias que sigan con los valores por defecto del semillero:
            // así no se sobrescriben cambios hechos a mano ni se repite el cambio en cada arranque.
            bool isDefault = pref.PaletaColor == "PastelSuave"
                             && pref.Tipografia == "Atkinson Hyperlegible"
                             && pref.ModoContraste == ModoContraste.Estandar
                             && pref.NivelEscalaFuente == 2;
            if (!isDefault) continue;

            pref.PaletaColor = p.paleta;
            pref.Tipografia = p.tipo;
            pref.ModoContraste = p.contraste;
            pref.NivelEscalaFuente = p.escala;
            pref.AudioActivo = p.audio;
            pref.VolumenAudio = p.vol;
            pref.TextoAVoz = p.tts;
            pref.AnimacionesReducidas = p.anim;
            pref.OcultarTemporizadores = p.ocultar;
            pref.ModoTemporizador = p.modo;
            pref.SegundosAvisoPrevio = p.aviso;
            pref.MayorEspacioEntreLetras = p.espacio;
            pref.UpdatedAt = DateTime.UtcNow;
        }

        await SaveIfPendingAsync(context);
    }

    // ------------------------------------------------------------------
    // Utilidades
    // ------------------------------------------------------------------
    private static Task<StudentProfile?> FindStudentAsync(ApplicationDbContext context, string codigo)
        => context.StudentProfiles.FirstOrDefaultAsync(s => s.CodigoEstudiante == codigo);

    /// <summary>Random determinista por clave (hash FNV-1a estable; string.GetHashCode no lo es entre procesos).</summary>
    private static Random Rng(string key)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in key)
            {
                hash = (hash ^ ch) * 16777619;
            }
            return new Random(BaseSeed ^ (int)hash);
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
