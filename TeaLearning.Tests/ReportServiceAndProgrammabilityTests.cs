using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Data.Context;
using TeaLearning.Data.SqlScripts;
using TeaLearning.Services.DTOs.Reports;
using TeaLearning.Services.Services;
using Xunit;

namespace TeaLearning.Tests;

public class ReportServiceAndProgrammabilityTests
{
    [Fact]
    public void SqlProgrammabilityDefinitions_Contains5TriggersAnd5ComplexStoredProcedures()
    {
        var batches = SqlProgrammabilityDefinitions.GetAllCreateOrAlterBatches();
        Assert.Equal(10, batches.Count);

        // Primeros 5: Triggers (INSERT, UPDATE, DELETE)
        Assert.Contains("TR_ActivityExecutions_AfterInsert_SyncAndAlert", batches[0]);
        Assert.Contains("AFTER INSERT", batches[0]);

        Assert.Contains("TR_ActivityAssignments_AfterUpdate_GradeAndRoutine", batches[1]);
        Assert.Contains("AFTER UPDATE", batches[1]);

        Assert.Contains("TR_SensoryPreferences_AfterUpsert_EnforceAndLog", batches[2]);
        Assert.Contains("AFTER INSERT, UPDATE", batches[2]);

        Assert.Contains("TR_Activities_AfterUpdate_CascadeDeactivation", batches[3]);
        Assert.Contains("AFTER UPDATE", batches[3]);

        Assert.Contains("TR_ActivityAssignments_AfterDelete_PreserveAndReorder", batches[4]);
        Assert.Contains("AFTER DELETE", batches[4]);

        // Últimos 5: Stored Procedures complejos (deben incluir variables, tablas temporales y ciclos WHILE)
        string[] expectedSpNames =
        [
            "sp_GenerarReporteIntegralEstudiante",
            "sp_GenerarReporteConsolidadoAula",
            "sp_AsignarRutinaAdaptativaPorAula",
            "sp_EvaluarAlertasYRecomendacionesTEA",
            "sp_RecalcularProgresoYLogrosEstudiante"
        ];

        for (int i = 0; i < 5; i++)
        {
            string spSql = batches[5 + i];
            Assert.Contains(expectedSpNames[i], spSql);
            Assert.Contains("DECLARE @", spSql);
            Assert.Contains("CREATE TABLE #", spSql);
            Assert.Contains("WHILE ", spSql);
        }
    }

    [Fact]
    public async Task ReportService_EnforcesGuardianPrivacyAndAuthorization_InMemory()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ReportService_Privacy_{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);

        var teacherUser = new ApplicationUser { Id = Guid.NewGuid(), UserName = "doc@test.com", Email = "doc@test.com", Nombres = "Laura", Apellidos = "Gómez" };
        var guardianUser = new ApplicationUser { Id = Guid.NewGuid(), UserName = "acu@test.com", Email = "acu@test.com", Nombres = "Carlos", Apellidos = "Pérez" };
        var otherGuardianUser = new ApplicationUser { Id = Guid.NewGuid(), UserName = "other@test.com", Email = "other@test.com", Nombres = "Otro", Apellidos = "Acudiente" };
        var studentUser = new ApplicationUser { Id = Guid.NewGuid(), UserName = "est@test.com", Email = "est@test.com", Nombres = "Mateo", Apellidos = "Pérez" };

        var teacherProfile = new TeacherProfile { Id = Guid.NewGuid(), UserId = teacherUser.Id, User = teacherUser, Especialidad = "TEA Nivel 1" };
        var guardianProfile = new GuardianProfile { Id = Guid.NewGuid(), UserId = guardianUser.Id, User = guardianUser, Parentesco = "Padre", TelefonoContacto = "3001234567" };
        var otherGuardianProfile = new GuardianProfile { Id = Guid.NewGuid(), UserId = otherGuardianUser.Id, User = otherGuardianUser, Parentesco = "Madre", TelefonoContacto = "3009999999" };
        var classroom = new Classroom { Id = Guid.NewGuid(), Nombre = "Aula Sendero A", Grado = "2°", AnioLectivo = 2026, TeacherProfileId = teacherProfile.Id, TeacherProfile = teacherProfile };

        var studentProfile = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = studentUser.Id,
            User = studentUser,
            CodigoEstudiante = "EST-TEA-001",
            FechaNacimiento = new DateTime(2017, 5, 10),
            ClassroomId = classroom.Id,
            Classroom = classroom,
            GuardianProfileId = guardianProfile.Id,
            GuardianProfile = guardianProfile
        };

        var module = new LearningModule { Id = Guid.NewGuid(), Titulo = "Módulo Rutinas", Descripcion = "Desc", Orden = 1 };
        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Titulo = "Secuencia Mañana",
            Descripcion = "Desc",
            TipoActividad = TipoActividad.SecuenciaLogica,
            ObjetivoPedagogico = "Obj",
            ConfigJson = "{\"pasos\":[\"Paso 1\",\"Paso 2\"]}",
            ModuloId = module.Id,
            Modulo = module,
            DocenteId = teacherProfile.Id
        };

        var assignment = new ActivityAssignment
        {
            Id = Guid.NewGuid(),
            ActivityId = activity.Id,
            Activity = activity,
            StudentProfileId = studentProfile.Id,
            FechaInicio = DateTime.UtcNow.Date,
            OrdenSecuencial = 1,
            Estado = EstadoAsignacion.Completada,
            Calificacion = 4.5m,
            ComentarioCalificacion = "Excelente trabajo"
        };

        var execution = new ActivityExecution
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignment.Id,
            StudentProfileId = studentProfile.Id,
            CantidadAciertos = 5,
            CantidadIntentosFallidos = 1,
            TiempoRespuestaSegundos = 45,
            CompletadaExitosamente = true,
            FechaRegistro = DateTime.UtcNow
        };

        var publicNote = new PedagogicalNote
        {
            Id = Guid.NewGuid(),
            StudentProfileId = studentProfile.Id,
            TeacherProfileId = teacherProfile.Id,
            Titulo = "Nota visible para familia",
            Contenido = "Avance positivo en casa",
            VisibleAcudiente = true,
            FechaRegistro = DateTime.UtcNow
        };

        var privateNote = new PedagogicalNote
        {
            Id = Guid.NewGuid(),
            StudentProfileId = studentProfile.Id,
            TeacherProfileId = teacherProfile.Id,
            Titulo = "Nota clínica interna",
            Contenido = "Solo para docente",
            VisibleAcudiente = false,
            FechaRegistro = DateTime.UtcNow
        };

        context.Users.AddRange(teacherUser, guardianUser, otherGuardianUser, studentUser);
        context.TeacherProfiles.Add(teacherProfile);
        context.GuardianProfiles.AddRange(guardianProfile, otherGuardianProfile);
        context.Classrooms.Add(classroom);
        context.StudentProfiles.Add(studentProfile);
        context.LearningModules.Add(module);
        context.Activities.Add(activity);
        context.ActivityAssignments.Add(assignment);
        context.ActivityExecutions.Add(execution);
        context.PedagogicalNotes.AddRange(publicNote, privateNote);
        await context.SaveChangesAsync();

        var service = new ReportService(context);

        // 1. Docente ve tanto notas públicas como privadas
        var teacherRes = await service.GetStudentComprehensiveReportAsync(studentProfile.Id, teacherUser.Id, isGuardian: false);
        Assert.True(teacherRes.Success);
        Assert.Equal(2, teacherRes.Data!.NotasPedagogicas.Count);

        // 2. Acudiente vinculado solo ve notas con VisibleAcudiente = true
        var guardianRes = await service.GetStudentComprehensiveReportAsync(studentProfile.Id, guardianUser.Id, isGuardian: true);
        Assert.True(guardianRes.Success);
        Assert.Single(guardianRes.Data!.NotasPedagogicas);
        Assert.All(guardianRes.Data.NotasPedagogicas, n => Assert.True(n.VisibleAcudiente));

        // 3. Acudiente no vinculado recibe rechazo de autorización
        var unauthorizedRes = await service.GetStudentComprehensiveReportAsync(studentProfile.Id, otherGuardianUser.Id, isGuardian: true);
        Assert.False(unauthorizedRes.Success);
        Assert.Contains("autorización", unauthorizedRes.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SqlServer_All5TriggersAnd5StoredProcedures_ExecuteEndToEnd()
    {
        string dbName = $"TeaLearning_ProgTest_{Guid.NewGuid():N}";
        string connString = $"Server=(localdb)\\mssqllocaldb;Database={dbName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connString)
            .Options;

        await using var context = new ApplicationDbContext(options);

        try
        {
            await context.Database.MigrateAsync();

            var teacherUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "docente.sp@test.com",
                NormalizedUserName = "DOCENTE.SP@TEST.COM",
                Email = "docente.sp@test.com",
                NormalizedEmail = "DOCENTE.SP@TEST.COM",
                Nombres = "Laura",
                Apellidos = "Gómez"
            };
            var guardianUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "acudiente.sp@test.com",
                NormalizedUserName = "ACUDIENTE.SP@TEST.COM",
                Email = "acudiente.sp@test.com",
                NormalizedEmail = "ACUDIENTE.SP@TEST.COM",
                Nombres = "Carlos",
                Apellidos = "Pérez"
            };
            var studentUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "estudiante.sp@test.com",
                NormalizedUserName = "ESTUDIANTE.SP@TEST.COM",
                Email = "estudiante.sp@test.com",
                NormalizedEmail = "ESTUDIANTE.SP@TEST.COM",
                Nombres = "Mateo",
                Apellidos = "Pérez"
            };

            context.Users.AddRange(teacherUser, guardianUser, studentUser);
            await context.SaveChangesAsync();

            var teacher = new TeacherProfile
            {
                Id = Guid.NewGuid(),
                UserId = teacherUser.Id,
                Especialidad = "Educación Especial TEA Nivel 1"
            };
            var guardian = new GuardianProfile
            {
                Id = Guid.NewGuid(),
                UserId = guardianUser.Id,
                Parentesco = "Padre",
                TelefonoContacto = "3001112233"
            };
            context.TeacherProfiles.Add(teacher);
            context.GuardianProfiles.Add(guardian);
            await context.SaveChangesAsync();

            var classroom = new Classroom
            {
                Id = Guid.NewGuid(),
                Nombre = "Aula TEA Prueba",
                Grado = "Primero",
                AnioLectivo = 2026,
                TeacherProfileId = teacher.Id
            };
            context.Classrooms.Add(classroom);
            await context.SaveChangesAsync();

            var student = new StudentProfile
            {
                Id = Guid.NewGuid(),
                UserId = studentUser.Id,
                CodigoEstudiante = "EST-SP-001",
                FechaNacimiento = new DateTime(2017, 4, 15),
                ClassroomId = classroom.Id,
                GuardianProfileId = guardian.Id,
                ObservacionesMedicasTEA = "TEA Nivel 1 con alta receptividad visual."
            };
            context.StudentProfiles.Add(student);
            await context.SaveChangesAsync();

            // -----------------------------------------------------------------
            // PRUEBA TRIGGER 3: TR_SensoryPreferences_AfterUpsert_EnforceAndLog
            // -----------------------------------------------------------------
            var pref = new SensoryPreference
            {
                Id = Guid.NewGuid(),
                StudentProfileId = student.Id,
                PaletaColor = "PastelSuave",
                Tipografia = "Atkinson Hyperlegible",
                AudioActivo = false,
                VolumenAudio = 85, // Debe ser ajustado a 0 por el Trigger porque AudioActivo = false
                NivelEscalaFuente = 5, // Debe ser ajustado a 3 (máximo) por el Trigger
                ModoContraste = ModoContraste.Estandar,
                OcultarTemporizadores = true,
                ModoTemporizador = "estandar", // Debe ser ajustado a 'sin-temporizador' por el Trigger
                SegundosAvisoPrevio = 2
            };
            context.SensoryPreferences.Add(pref);
            await context.SaveChangesAsync();

            await context.Entry(pref).ReloadAsync();
            Assert.Equal(0, pref.VolumenAudio);
            Assert.Equal(3, pref.NivelEscalaFuente);
            Assert.Equal("sin-temporizador", pref.ModoTemporizador);

            pref.PaletaColor = "VerdeBosque";
            await context.SaveChangesAsync();

            bool sensoryNoteCreated = await context.PedagogicalNotes
                .AnyAsync(n => n.StudentProfileId == student.Id && n.Titulo.Contains("[AUTO-SENSORIAL]"));
            Assert.True(sensoryNoteCreated);

            // Crear módulo y 3 actividades
            var module = new LearningModule
            {
                Id = Guid.NewGuid(),
                Titulo = "Módulo 1: Rutinas Diarias",
                Descripcion = "Secuencias cotidianas",
                Orden = 1
            };
            context.LearningModules.Add(module);
            await context.SaveChangesAsync();

            var act1 = new Activity
            {
                Id = Guid.NewGuid(),
                Titulo = "Cepillado de dientes",
                Descripcion = "Secuencia de higiene",
                TipoActividad = TipoActividad.SecuenciaLogica,
                ObjetivoPedagogico = "Autonomía",
                ConfigJson = "{\"pasos\":[\"Cepillo\",\"Agua\"]}",
                ModuloId = module.Id,
                DocenteId = teacher.Id,
                IsActive = true
            };
            var act2 = new Activity
            {
                Id = Guid.NewGuid(),
                Titulo = "Emparejar emociones",
                Descripcion = "Asociación visual",
                TipoActividad = TipoActividad.EmparejamientoVisual,
                ObjetivoPedagogico = "Reconocimiento emocional",
                ConfigJson = "{\"parejas\":[{\"leftLabel\":\"Feliz\",\"rightLabel\":\"Sonrisa\"},{\"leftLabel\":\"Calma\",\"rightLabel\":\"Respirar\"}]}",
                ModuloId = module.Id,
                DocenteId = teacher.Id,
                IsActive = true
            };
            var act3 = new Activity
            {
                Id = Guid.NewGuid(),
                Titulo = "Saludo escolar",
                Descripcion = "Rutina social",
                TipoActividad = TipoActividad.RutinaSocial,
                ObjetivoPedagogico = "Interacción",
                ConfigJson = "{\"pasos\":[\"Mirar\",\"Decir hola\"]}",
                ModuloId = module.Id,
                DocenteId = teacher.Id,
                IsActive = true
            };
            context.Activities.AddRange(act1, act2, act3);
            await context.SaveChangesAsync();

            var today = DateTime.UtcNow.Date;
            var assign1 = new ActivityAssignment
            {
                Id = Guid.NewGuid(),
                ActivityId = act1.Id,
                StudentProfileId = student.Id,
                FechaInicio = today,
                OrdenSecuencial = 1,
                Prioridad = PrioridadActividad.Media,
                Estado = EstadoAsignacion.Pendiente
            };
            var assign2 = new ActivityAssignment
            {
                Id = Guid.NewGuid(),
                ActivityId = act2.Id,
                StudentProfileId = student.Id,
                FechaInicio = today,
                OrdenSecuencial = 2,
                Prioridad = PrioridadActividad.Baja,
                Estado = EstadoAsignacion.Pendiente
            };
            var assign3 = new ActivityAssignment
            {
                Id = Guid.NewGuid(),
                ActivityId = act3.Id,
                StudentProfileId = student.Id,
                FechaInicio = today,
                OrdenSecuencial = 3,
                Prioridad = PrioridadActividad.Baja,
                Estado = EstadoAsignacion.Pendiente
            };
            context.ActivityAssignments.AddRange(assign1, assign2, assign3);
            await context.SaveChangesAsync();

            // -----------------------------------------------------------------
            // PRUEBA TRIGGER 1: TR_ActivityExecutions_AfterInsert_SyncAndAlert
            // -----------------------------------------------------------------
            var failedExec = new ActivityExecution
            {
                Id = Guid.NewGuid(),
                AssignmentId = assign1.Id,
                StudentProfileId = student.Id,
                CantidadAciertos = 1,
                CantidadIntentosFallidos = 5, // >= 4 fallos sin completar -> dispara alerta automática
                TiempoRespuestaSegundos = 95,
                CompletadaExitosamente = false,
                FechaRegistro = DateTime.UtcNow
            };
            context.ActivityExecutions.Add(failedExec);
            await context.SaveChangesAsync();

            await context.Entry(assign1).ReloadAsync();
            Assert.Equal(EstadoAsignacion.EnProgreso, assign1.Estado);
            Assert.True(await context.PedagogicalNotes.AnyAsync(n =>
                n.StudentProfileId == student.Id && n.Titulo.Contains("[AUTO-TRIGGER]")));

            var successExec = new ActivityExecution
            {
                Id = Guid.NewGuid(),
                AssignmentId = assign1.Id,
                StudentProfileId = student.Id,
                CantidadAciertos = 5,
                CantidadIntentosFallidos = 0,
                TiempoRespuestaSegundos = 50,
                CompletadaExitosamente = true,
                FechaRegistro = DateTime.UtcNow
            };
            context.ActivityExecutions.Add(successExec);
            await context.SaveChangesAsync();

            await context.Entry(assign1).ReloadAsync();
            Assert.Equal(EstadoAsignacion.Completada, assign1.Estado);

            // -----------------------------------------------------------------
            // PRUEBA TRIGGER 2: TR_ActivityAssignments_AfterUpdate_GradeAndRoutine
            // -----------------------------------------------------------------
            assign1.Calificacion = 2.5m; // < 3.0 -> genera nota de refuerzo y agenda asignación de refuerzo
            assign1.CalificadoPorDocenteId = teacher.Id;
            assign1.ComentarioCalificacion = "Requiere repasar el orden de los pasos.";
            await context.SaveChangesAsync();

            Assert.True(await context.PedagogicalNotes.AnyAsync(n =>
                n.AssignmentId == assign1.Id && n.Titulo.Contains("[AUTO-CALIFICACIÓN]")));

            bool reinforcementScheduled = await context.ActivityAssignments.AnyAsync(a =>
                a.StudentProfileId == student.Id &&
                a.ActivityId == act1.Id &&
                a.Id != assign1.Id &&
                a.Prioridad == PrioridadActividad.Alta);
            Assert.True(reinforcementScheduled);

            // -----------------------------------------------------------------
            // PRUEBA TRIGGER 4: TR_Activities_AfterUpdate_CascadeDeactivation
            // -----------------------------------------------------------------
            act2.IsActive = false;
            await context.SaveChangesAsync();

            await context.Entry(assign2).ReloadAsync();
            await context.Entry(assign3).ReloadAsync();
            Assert.False(assign2.IsActive);
            Assert.Equal(2, assign3.OrdenSecuencial); // Reordenado de 3 a 2 sin huecos

            // -----------------------------------------------------------------
            // PRUEBA TRIGGER 5: TR_ActivityAssignments_AfterDelete_PreserveAndReorder
            // -----------------------------------------------------------------
            var linkedNote = new PedagogicalNote
            {
                Id = Guid.NewGuid(),
                StudentProfileId = student.Id,
                TeacherProfileId = teacher.Id,
                AssignmentId = assign3.Id,
                Titulo = "Nota vinculada a assign3",
                Contenido = "Observación original."
            };
            context.PedagogicalNotes.Add(linkedNote);
            await context.SaveChangesAsync();

            context.ActivityAssignments.Remove(assign3);
            await context.SaveChangesAsync();

            await context.Entry(linkedNote).ReloadAsync();
            Assert.Null(linkedNote.AssignmentId);
            Assert.Contains("[Ref: Asignación eliminada]", linkedNote.Contenido);

            // -----------------------------------------------------------------
            // PRUEBA DE LOS 5 STORED PROCEDURES VÍA REPORTSERVICE
            // -----------------------------------------------------------------
            var reportService = new ReportService(context);

            // SP 1: sp_GenerarReporteIntegralEstudiante
            var sp1Res = await reportService.GetStudentComprehensiveReportAsync(student.Id, teacherUser.Id, isGuardian: false, diasVentana: 14);
            Assert.True(sp1Res.Success);
            Assert.NotNull(sp1Res.Data);
            Assert.Equal("EST-SP-001", sp1Res.Data!.CodigoEstudiante);
            Assert.Equal(14, sp1Res.Data.SerieDiaria.Count);
            Assert.Equal(4, sp1Res.Data.DesglosePorTipo.Count);

            // SP 2: sp_GenerarReporteConsolidadoAula
            var sp2Res = await reportService.GetClassroomConsolidatedReportAsync(classroom.Id, teacherUser.Id, diasVentana: 30);
            Assert.True(sp2Res.Success);
            Assert.NotNull(sp2Res.Data);
            Assert.Single(sp2Res.Data!.Estudiantes);
            Assert.Equal(4, sp2Res.Data.EvolucionSemanal.Count);

            // SP 3: sp_AsignarRutinaAdaptativaPorAula
            var sp3Res = await reportService.AssignAdaptiveRoutineByClassroomAsync(
                teacherUser.Id,
                new AdaptiveRoutineRequestDto { ClassroomId = classroom.Id, FechaInicio = today.AddDays(3), MaxActividadesPorEstudiante = 2 });
            Assert.True(sp3Res.Success);
            Assert.NotNull(sp3Res.Data);

            // SP 4: sp_EvaluarAlertasYRecomendacionesTEA
            var sp4Res = await reportService.EvaluateTeaAlertsAndRecommendationsAsync(
                teacherUser.Id,
                new EvaluateTeaAlertsRequestDto { ClassroomId = classroom.Id, DiasAnalisis = 14, RegistrarNotasAutomaticas = true });
            Assert.True(sp4Res.Success);
            Assert.NotNull(sp4Res.Data);

            // SP 5: sp_RecalcularProgresoYLogrosEstudiante
            var sp5Res = await reportService.RecalculateStudentProgressAndAchievementsAsync(student.Id);
            Assert.True(sp5Res.Success);
            Assert.NotNull(sp5Res.Data);
            Assert.Equal(7, sp5Res.Data!.Insignias.Count);
            Assert.Contains(sp5Res.Data.Insignias, b => b.Clave == "primer_paso" && b.Desbloqueado);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }
}
