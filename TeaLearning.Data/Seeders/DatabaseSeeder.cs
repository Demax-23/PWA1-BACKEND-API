using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Data.Context;

namespace TeaLearning.Data.Seeders;

public static class DatabaseSeeder
{
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
        var adminEmail = "admin@tealearning.com";
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
        var teacherEmail = "docente@tealearning.com";
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
        var guardianEmail = "acudiente@tealearning.com";
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
                    OcultarTemporizadores = true
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
    }
}
