using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Data.Context;

namespace TeaLearning.Data.Seeders;

public static class DatabaseSeeder
{
    // Correos reales (alias de Gmail) para poder recibir el restablecimiento de contraseña.
    // El estudiante conserva su correo ficticio: no recibe correos, su contraseña la restablece el acudiente.
    private const string AdminEmail = "tealearning.rol+administrador@gmail.com";
    private const string TeacherEmail = "tealearning.rol+docente@gmail.com";
    private const string GuardianEmail = "tealearning.rol+acudiente@gmail.com";

    private const string LegacyAdminEmail = "admin@tealearning.com";
    private const string LegacyTeacherEmail = "docente@tealearning.com";
    private const string LegacyGuardianEmail = "acudiente@tealearning.com";

    /// <summary>
    /// Migra de forma idempotente un usuario sembrado con el correo antiguo (@tealearning.com) al correo real.
    /// No hace nada si el usuario antiguo no existe o si ya existe uno con el correo nuevo.
    /// </summary>
    private static async Task RenameLegacyUserEmailAsync(
        UserManager<ApplicationUser> userManager,
        string legacyEmail,
        string newEmail)
    {
        var legacyUser = await userManager.FindByEmailAsync(legacyEmail);
        if (legacyUser == null || await userManager.FindByEmailAsync(newEmail) != null)
        {
            return;
        }

        legacyUser.Email = newEmail;
        legacyUser.NormalizedEmail = userManager.NormalizeEmail(newEmail);
        legacyUser.UserName = newEmail;
        legacyUser.NormalizedUserName = userManager.NormalizeName(newEmail);
        await userManager.UpdateAsync(legacyUser);
    }

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>();

        // Aplicar migraciones pendientes
        if ((await context.Database.GetPendingMigrationsAsync()).Any())
        {
            await context.Database.MigrateAsync();
        }

        // 1. Sembrar Roles
        string[] roles = ["Administrador", "Docente", "Estudiante", "Acudiente"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        // 2. Sembrar Administrador por defecto
        var adminEmail = AdminEmail;
        await RenameLegacyUserEmailAsync(userManager, LegacyAdminEmail, adminEmail);
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                Nombres = "Administrador",
                Apellidos = "Sistema",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(adminUser, "Admin123!*");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Administrador");
            }
        }
        else if (string.IsNullOrEmpty(adminUser.PasswordHash) || !adminUser.PasswordHash.StartsWith("$2"))
        {
            adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, "Admin123!*");
            await userManager.UpdateAsync(adminUser);
        }

        // 3. Sembrar Docente de prueba
        var teacherEmail = TeacherEmail;
        await RenameLegacyUserEmailAsync(userManager, LegacyTeacherEmail, teacherEmail);
        var teacherUser = await userManager.FindByEmailAsync(teacherEmail);
        TeacherProfile? teacherProfile = null;
        if (teacherUser == null)
        {
            teacherUser = new ApplicationUser
            {
                UserName = teacherEmail,
                Email = teacherEmail,
                EmailConfirmed = true,
                Nombres = "Laura",
                Apellidos = "Gómez",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(teacherUser, "Docente123!*");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(teacherUser, "Docente");

                teacherProfile = new TeacherProfile
                {
                    UserId = teacherUser.Id,
                    Especialidad = "Educación Especial y TEA Nivel 1",
                    NumeroRegistroProfesional = "REG-DOC-2026-01"
                };
                context.TeacherProfiles.Add(teacherProfile);
                await context.SaveChangesAsync();
            }
        }
        else
        {
            if (string.IsNullOrEmpty(teacherUser.PasswordHash) || !teacherUser.PasswordHash.StartsWith("$2"))
            {
                teacherUser.PasswordHash = passwordHasher.HashPassword(teacherUser, "Docente123!*");
                await userManager.UpdateAsync(teacherUser);
            }
            teacherProfile = await context.TeacherProfiles.FirstOrDefaultAsync(t => t.UserId == teacherUser.Id);
        }

        // 4. Sembrar Aula para el Docente
        Classroom? classroom = null;
        if (teacherProfile != null)
        {
            classroom = await context.Classrooms.FirstOrDefaultAsync(c => c.TeacherProfileId == teacherProfile.Id);
            if (classroom == null)
            {
                classroom = new Classroom
                {
                    Nombre = "Aula Azul - Estimulación Cognitiva",
                    Grado = "Primaria Inicial",
                    AnioLectivo = DateTime.UtcNow.Year,
                    TeacherProfileId = teacherProfile.Id
                };
                context.Classrooms.Add(classroom);
                await context.SaveChangesAsync();
            }
        }

        // 5. Sembrar Acudiente de prueba
        var guardianEmail = GuardianEmail;
        await RenameLegacyUserEmailAsync(userManager, LegacyGuardianEmail, guardianEmail);
        var guardianUser = await userManager.FindByEmailAsync(guardianEmail);
        GuardianProfile? guardianProfile = null;
        if (guardianUser == null)
        {
            guardianUser = new ApplicationUser
            {
                UserName = guardianEmail,
                Email = guardianEmail,
                EmailConfirmed = true,
                Nombres = "Carlos",
                Apellidos = "Pérez",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(guardianUser, "Acudiente123!*");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(guardianUser, "Acudiente");

                guardianProfile = new GuardianProfile
                {
                    UserId = guardianUser.Id,
                    TelefonoContacto = "+57 300 123 4567",
                    Parentesco = "Padre",
                    Direccion = "Calle 100 # 15-20"
                };
                context.GuardianProfiles.Add(guardianProfile);
                await context.SaveChangesAsync();
            }
        }
        else
        {
            if (string.IsNullOrEmpty(guardianUser.PasswordHash) || !guardianUser.PasswordHash.StartsWith("$2"))
            {
                guardianUser.PasswordHash = passwordHasher.HashPassword(guardianUser, "Acudiente123!*");
                await userManager.UpdateAsync(guardianUser);
            }
            guardianProfile = await context.GuardianProfiles.FirstOrDefaultAsync(g => g.UserId == guardianUser.Id);
        }

        // 6. Sembrar Estudiante de prueba con SensoryPreference
        var studentEmail = "estudiante@tealearning.com";
        var studentUser = await userManager.FindByEmailAsync(studentEmail);
        StudentProfile? studentProfile = null;
        if (studentUser == null)
        {
            studentUser = new ApplicationUser
            {
                UserName = studentEmail,
                Email = studentEmail,
                EmailConfirmed = true,
                Nombres = "Mateo",
                Apellidos = "Pérez",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(studentUser, "Estudiante123!*");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(studentUser, "Estudiante");

                studentProfile = new StudentProfile
                {
                    UserId = studentUser.Id,
                    CodigoEstudiante = "EST-TEA-001",
                    FechaNacimiento = new DateTime(2017, 5, 15),
                    ObservacionesMedicasTEA = "Diagnóstico TEA Nivel 1. Excelente respuesta a apoyos visuales y rutinas predecibles.",
                    ClassroomId = classroom?.Id,
                    GuardianProfileId = guardianProfile?.Id
                };
                context.StudentProfiles.Add(studentProfile);
                await context.SaveChangesAsync();

                // Crear SensoryPreference por defecto para el estudiante
                var sensoryPreference = new SensoryPreference
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
                context.SensoryPreferences.Add(sensoryPreference);
                await context.SaveChangesAsync();
            }
        }
        else
        {
            if (string.IsNullOrEmpty(studentUser.PasswordHash) || !studentUser.PasswordHash.StartsWith("$2"))
            {
                studentUser.PasswordHash = passwordHasher.HashPassword(studentUser, "Estudiante123!*");
                await userManager.UpdateAsync(studentUser);
            }
            studentProfile = await context.StudentProfiles.FirstOrDefaultAsync(s => s.UserId == studentUser.Id);
        }

        // 7. Sembrar Categorías de Aprendizaje (HU-007)
        var catComunicacion = await context.LearningCategories.FirstOrDefaultAsync(c => c.Nombre == "Comunicación");
        if (catComunicacion == null)
        {
            catComunicacion = new LearningCategory
            {
                Nombre = "Comunicación",
                Descripcion = "Habilidades para expresar necesidades, ideas y opiniones.",
                Color = "#2F6E5C",
                Orden = 1,
                IsActive = true
            };
            context.LearningCategories.Add(catComunicacion);
        }

        var catRutinas = await context.LearningCategories.FirstOrDefaultAsync(c => c.Nombre == "Rutinas diarias");
        if (catRutinas == null)
        {
            catRutinas = new LearningCategory
            {
                Nombre = "Rutinas diarias",
                Descripcion = "Actividades de la vida cotidiana: higiene, orden, tiempo.",
                Color = "#9C7A4C",
                Orden = 2,
                IsActive = true
            };
            context.LearningCategories.Add(catRutinas);
        }

        var catSociales = await context.LearningCategories.FirstOrDefaultAsync(c => c.Nombre == "Habilidades sociales");
        if (catSociales == null)
        {
            catSociales = new LearningCategory
            {
                Nombre = "Habilidades sociales",
                Descripcion = "Interacción con pares y adultos, turnos, juego compartido.",
                Color = "#3B6D11",
                Orden = 3,
                IsActive = false
            };
            context.LearningCategories.Add(catSociales);
        }
        await context.SaveChangesAsync();

        // 8. Sembrar Módulo, Actividades y Asignaciones para la Rutina Diaria
        if (teacherProfile != null && studentProfile != null && !await context.LearningModules.AnyAsync())
        {
            var module = new LearningModule
            {
                Titulo = "Módulo 1: Identificación y Gestión Emocional",
                Descripcion = "Reconocimiento y comprensión de expresiones faciales y emociones básicas.",
                Icono = "smile",
                Orden = 1,
                CategoriaId = catComunicacion.Id
            };
            context.LearningModules.Add(module);
            await context.SaveChangesAsync();

            var act1 = new Activity
            {
                Titulo = "Emparejamiento de Emociones",
                Descripcion = "Asocia cada rostro ilustrado con su emoción correspondiente.",
                TipoActividad = TipoActividad.EmparejamientoVisual,
                ObjetivoPedagogico = "Fomentar la discriminación visual de estados de ánimo.",
                ConfigJson = "{\"pares\":[{\"emocion\":\"Feliz\",\"color\":\"#FFEB3B\"},{\"emocion\":\"Calmado\",\"color\":\"#4CAF50\"}]}",
                ModuloId = module.Id,
                DocenteId = teacherProfile.Id
            };

            var act2 = new Activity
            {
                Titulo = "Secuencia de la Mañana Escolar",
                Descripcion = "Ordena secuencialmente los pasos para comenzar la jornada en el colegio.",
                TipoActividad = TipoActividad.SecuenciaLogica,
                ObjetivoPedagogico = "Afianzar la estructura temporal y reducir ansiedad anticipatoria.",
                ConfigJson = "{\"pasos\":[\"Llegar al salón\",\"Saludar al docente\",\"Guardar la maleta\",\"Sentarse en el puesto\"]}",
                ModuloId = module.Id,
                DocenteId = teacherProfile.Id
            };

            context.Activities.AddRange(act1, act2);
            await context.SaveChangesAsync();

            var assignment1 = new ActivityAssignment
            {
                ActivityId = act1.Id,
                StudentProfileId = studentProfile.Id,
                FechaInicio = DateTime.UtcNow.Date,
                FechaLimite = DateTime.UtcNow.Date.AddDays(7),
                OrdenSecuencial = 1,
                Prioridad = PrioridadActividad.Alta,
                Estado = EstadoAsignacion.Pendiente
            };

            var assignment2 = new ActivityAssignment
            {
                ActivityId = act2.Id,
                StudentProfileId = studentProfile.Id,
                FechaInicio = DateTime.UtcNow.Date,
                FechaLimite = DateTime.UtcNow.Date.AddDays(7),
                OrdenSecuencial = 2,
                Prioridad = PrioridadActividad.Media,
                Estado = EstadoAsignacion.Pendiente
            };

            context.ActivityAssignments.AddRange(assignment1, assignment2);
            await context.SaveChangesAsync();
        }

        // Vincular módulos existentes que no tengan CategoriaId
        var unlinkedModules = await context.LearningModules.Where(m => m.CategoriaId == null).ToListAsync();
        if (unlinkedModules.Any() && catComunicacion != null)
        {
            foreach (var m in unlinkedModules)
            {
                m.CategoriaId = catComunicacion.Id;
            }
            await context.SaveChangesAsync();
        }

        // 9. Sembrar Ejecuciones y Notas Pedagógicas de Demostración Inicial (Sprint 3)
        if (studentProfile != null && !await context.ActivityExecutions.AnyAsync())
        {
            var existingAssignment = await context.ActivityAssignments.FirstOrDefaultAsync(a => a.StudentProfileId == studentProfile.Id);
            if (existingAssignment != null)
            {
                var exec1 = new ActivityExecution
                {
                    AssignmentId = existingAssignment.Id,
                    StudentProfileId = studentProfile.Id,
                    TiempoRespuestaSegundos = 42,
                    CantidadAciertos = 5,
                    CantidadIntentosFallidos = 1,
                    CompletadaExitosamente = true,
                    FechaRegistro = DateTime.UtcNow.AddDays(-2)
                };
                var exec2 = new ActivityExecution
                {
                    AssignmentId = existingAssignment.Id,
                    StudentProfileId = studentProfile.Id,
                    TiempoRespuestaSegundos = 35,
                    CantidadAciertos = 6,
                    CantidadIntentosFallidos = 0,
                    CompletadaExitosamente = true,
                    FechaRegistro = DateTime.UtcNow
                };
                existingAssignment.Estado = EstadoAsignacion.Completada;
                context.ActivityExecutions.AddRange(exec1, exec2);
                await context.SaveChangesAsync();
            }
        }

        if (studentProfile != null && teacherProfile != null && !await context.PedagogicalNotes.AnyAsync())
        {
            var note = new PedagogicalNote
            {
                StudentProfileId = studentProfile.Id,
                TeacherProfileId = teacherProfile.Id,
                Contenido = "Mateo demostró un excelente avance en la identificación visual de emociones básicas. Mantuvo la atención durante toda la sesión y completó las secuencias con entusiasmo.",
                FechaRegistro = DateTime.UtcNow.AddDays(-1),
                IsActive = true
            };
            context.PedagogicalNotes.Add(note);
            await context.SaveChangesAsync();
        }

        // 10. Sembrar 10 estudiantes de prueba con datos aleatorios (semilla fija) para validar el panel de desempeño
        if (classroom != null && teacherProfile != null)
        {
            await SeedTestStudentsAsync(context, userManager, classroom, teacherProfile, guardianProfile);
        }
    }

    private static async Task SeedTestStudentsAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        Classroom classroom,
        TeacherProfile teacherProfile,
        GuardianProfile? guardianProfile)
    {
        const int total = 10;
        const int firstNumber = 3; // estudiante@ y estudiante2@ ya existen: se continúa con estudiante3@ ... estudiante12@
        var rnd = new Random(2026);

        string[] nombres = ["Sofía", "Santiago", "Valentina", "Juan", "Isabella", "Samuel", "Camila", "Emmanuel", "Luciana", "Tomás", "Mariana", "Nicolás"];
        string[] apellidos = ["Rodríguez", "Martínez", "López", "García", "Hernández", "Ramírez", "Torres", "Vargas", "Castro", "Moreno", "Rojas", "Díaz"];

        // Módulo y actividades (se cubren los 4 tipos de actividad para el desglose por tipo)
        var module = await context.LearningModules.OrderBy(m => m.Orden).FirstOrDefaultAsync();
        if (module == null) return;

        async Task<Activity> EnsureActivityAsync(string titulo, TipoActividad tipo, string config)
        {
            var existing = await context.Activities.FirstOrDefaultAsync(a => a.Titulo == titulo);
            if (existing != null) return existing;
            var act = new Activity
            {
                Titulo = titulo,
                Descripcion = $"Actividad de prueba ({tipo}).",
                TipoActividad = tipo,
                ObjetivoPedagogico = "Actividad de demostración para validar la analítica del aula.",
                ConfigJson = config,
                ModuloId = module.Id,
                DocenteId = teacherProfile.Id
            };
            context.Activities.Add(act);
            await context.SaveChangesAsync();
            return act;
        }

        var activities = new List<Activity>
        {
            await EnsureActivityAsync("Secuencia de la Mañana Escolar", TipoActividad.SecuenciaLogica, "{\"pasos\":[\"Llegar\",\"Saludar\",\"Guardar la maleta\",\"Sentarse\"]}"),
            await EnsureActivityAsync("Emparejamiento de Emociones", TipoActividad.EmparejamientoVisual, "{\"pares\":[{\"emocion\":\"Feliz\",\"color\":\"#FFEB3B\"},{\"emocion\":\"Calmado\",\"color\":\"#4CAF50\"}]}"),
            await EnsureActivityAsync("Reconociendo Emociones (Prueba)", TipoActividad.AsociacionEmocional, "{\"emociones\":[\"Alegría\",\"Tristeza\",\"Enojo\",\"Sorpresa\"]}"),
            await EnsureActivityAsync("Saludos y Turnos (Prueba)", TipoActividad.RutinaSocial, "{\"escenas\":[\"Saludar a un compañero\",\"Esperar el turno\"]}")
        };

        for (int i = 0; i < total; i++)
        {
            int number = firstNumber + i;
            var email = $"estudiante{number}@tealearning.com";
            var codigo = $"EST-TEA-{number:000}";

            // Siempre se consumen los mismos valores aleatorios por iteración para mantener el resultado reproducible
            var nombre = nombres[rnd.Next(nombres.Length)];
            var apellido = $"{apellidos[rnd.Next(apellidos.Length)]} {apellidos[rnd.Next(apellidos.Length)]}";
            var nacimiento = new DateTime(rnd.Next(2015, 2019), rnd.Next(1, 13), rnd.Next(1, 28));
            var execCount = rnd.Next(2, 7);
            var assignCount = rnd.Next(3, 5);

            if (await userManager.FindByEmailAsync(email) != null) continue;

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                Nombres = nombre,
                Apellidos = apellido,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(user, "Estudiante123!*");
            if (!result.Succeeded) continue;
            await userManager.AddToRoleAsync(user, "Estudiante");

            var profile = new StudentProfile
            {
                UserId = user.Id,
                CodigoEstudiante = codigo,
                FechaNacimiento = nacimiento,
                ObservacionesMedicasTEA = "Estudiante de prueba con datos generados aleatoriamente (TEA Nivel 1).",
                ClassroomId = classroom.Id,
                GuardianProfileId = guardianProfile?.Id
            };
            context.StudentProfiles.Add(profile);
            await context.SaveChangesAsync();

            context.SensoryPreferences.Add(new SensoryPreference
            {
                StudentProfileId = profile.Id,
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
            });

            var assignments = new List<ActivityAssignment>();
            for (int a = 0; a < assignCount; a++)
            {
                assignments.Add(new ActivityAssignment
                {
                    ActivityId = activities[(i + a) % activities.Count].Id,
                    StudentProfileId = profile.Id,
                    FechaInicio = DateTime.UtcNow.Date.AddDays(-28),
                    FechaLimite = DateTime.UtcNow.Date.AddDays(7),
                    OrdenSecuencial = a + 1,
                    Prioridad = (PrioridadActividad)rnd.Next(0, Enum.GetValues<PrioridadActividad>().Length),
                    Estado = EstadoAsignacion.Pendiente
                });
            }
            context.ActivityAssignments.AddRange(assignments);
            await context.SaveChangesAsync();

            // Casos borde para validar el tablero: i=0 sin ejecuciones, i=1 con 100%, i=2 con desempeño bajo
            if (i == 0) continue;

            var executions = new List<ActivityExecution>();
            for (int e = 0; e < execCount; e++)
            {
                var assignment = assignments[e % assignments.Count];
                int aciertos, fallos;
                bool completada;
                if (i == 1) { aciertos = rnd.Next(5, 11); fallos = 0; completada = true; }
                else if (i == 2) { aciertos = rnd.Next(1, 3); fallos = rnd.Next(5, 9); completada = rnd.Next(0, 4) == 0; }
                else { aciertos = rnd.Next(2, 11); fallos = rnd.Next(0, 7); completada = rnd.Next(0, 10) < 7; }

                executions.Add(new ActivityExecution
                {
                    AssignmentId = assignment.Id,
                    StudentProfileId = profile.Id,
                    TiempoRespuestaSegundos = rnd.Next(15, 121),
                    CantidadAciertos = aciertos,
                    CantidadIntentosFallidos = fallos,
                    CompletadaExitosamente = completada,
                    FechaRegistro = DateTime.UtcNow.AddDays(-rnd.Next(0, 28)).AddMinutes(-rnd.Next(0, 600))
                });
                if (completada) assignment.Estado = EstadoAsignacion.Completada;
                else if (assignment.Estado == EstadoAsignacion.Pendiente) assignment.Estado = EstadoAsignacion.EnProgreso;
            }
            context.ActivityExecutions.AddRange(executions);
            await context.SaveChangesAsync();
        }
    }
}
