using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using TeaLearning.Api.Middlewares;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Data.Context;
using TeaLearning.Data.Repositories;
using TeaLearning.Services.DTOs.Activities;
using TeaLearning.Services.DTOs.Assignments;
using TeaLearning.Services.DTOs.Execution;
using TeaLearning.Services.DTOs.Sensory;
using TeaLearning.Services.Services;

namespace TeaLearning.Tests;

public class SecurityAuditTests
{
    private static ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TeaSecurityTestDb_{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task SensoryPreference_ShouldRejectUnlinkedGuardian_BolaProtection()
    {
        using var context = CreateInMemoryContext();

        var guardianOwnerUserId = Guid.NewGuid();
        var attackerGuardianUserId = Guid.NewGuid();
        var studentUserId = Guid.NewGuid();

        var guardianProfile = new GuardianProfile
        {
            Id = Guid.NewGuid(),
            UserId = guardianOwnerUserId,
            Parentesco = "Madre",
            TelefonoContacto = "3001234567"
        };

        var student = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = studentUserId,
            CodigoEstudiante = "EST-SEC-01",
            GuardianProfileId = guardianProfile.Id,
            GuardianProfile = guardianProfile
        };

        var pref = new SensoryPreference
        {
            Id = Guid.NewGuid(),
            StudentProfileId = student.Id,
            PaletaColor = "PastelSuave",
            Tipografia = "Atkinson Hyperlegible",
            VolumenAudio = 60,
            NivelEscalaFuente = 2
        };

        context.GuardianProfiles.Add(guardianProfile);
        context.StudentProfiles.Add(student);
        context.SensoryPreferences.Add(pref);
        await context.SaveChangesAsync();

        var prefRepo = new SensoryPreferenceRepository(context);
        var studentRepo = new StudentRepository(context);
        var service = new SensoryPreferenceService(prefRepo, studentRepo);

        // Acudiente NO vinculado intenta leer preferencias sensoriales
        var getResult = await service.GetSensoryPreferencesByStudentIdAsync(
            student.Id,
            currentUserId: attackerGuardianUserId,
            isStaff: false);
        Assert.False(getResult.Success);
        Assert.Contains("No tiene autorización", getResult.Message);

        // Acudiente NO vinculado intenta modificar preferencias sensoriales
        var updateDto = new UpdateSensoryPreferenceDto
        {
            PaletaColor = "PastelSuave",
            Tipografia = "Atkinson Hyperlegible",
            VolumenAudio = 50,
            NivelEscalaFuente = 2
        };
        var putResult = await service.UpdateSensoryPreferencesAsync(
            student.Id,
            updateDto,
            currentUserId: attackerGuardianUserId,
            isAdmin: false);
        Assert.False(putResult.Success);
        Assert.Contains("No tiene autorización", putResult.Message);

        // Acudiente legítimo sí puede actualizar
        var allowedResult = await service.UpdateSensoryPreferencesAsync(
            student.Id,
            updateDto,
            currentUserId: guardianOwnerUserId,
            isAdmin: false);
        Assert.True(allowedResult.Success);
    }

    [Fact]
    public async Task SensoryPreference_ShouldRejectOtherStudent_BolaProtection()
    {
        using var context = CreateInMemoryContext();

        var student1UserId = Guid.NewGuid();
        var student2UserId = Guid.NewGuid();

        var student1 = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = student1UserId,
            CodigoEstudiante = "EST-SEC-02"
        };

        context.StudentProfiles.Add(student1);
        await context.SaveChangesAsync();

        var prefRepo = new SensoryPreferenceRepository(context);
        var studentRepo = new StudentRepository(context);
        var service = new SensoryPreferenceService(prefRepo, studentRepo);

        var result = await service.GetSensoryPreferencesByStudentIdAsync(
            student1.Id,
            currentUserId: student2UserId,
            isStaff: false);
        Assert.False(result.Success);
        Assert.Contains("No tiene autorización", result.Message);
    }

    [Fact]
    public async Task AssignmentService_ShouldEnforceBolaForGuardianAndTeacher()
    {
        using var context = CreateInMemoryContext();

        var teacher1User = new ApplicationUser { Id = Guid.NewGuid(), Nombres = "Docente", Apellidos = "Uno", Email = "doc1@tea.edu" };
        var teacher2User = new ApplicationUser { Id = Guid.NewGuid(), Nombres = "Docente", Apellidos = "Dos", Email = "doc2@tea.edu" };
        var guardianOwnerUser = new ApplicationUser { Id = Guid.NewGuid(), Nombres = "Acudiente", Apellidos = "Uno", Email = "acu1@tea.edu" };
        var attackerGuardianUserId = Guid.NewGuid();

        var teacher1 = new TeacherProfile { Id = Guid.NewGuid(), UserId = teacher1User.Id, User = teacher1User, Especialidad = "TEA", IsActive = true };
        var teacher2 = new TeacherProfile { Id = Guid.NewGuid(), UserId = teacher2User.Id, User = teacher2User, Especialidad = "TEA", IsActive = true };
        var guardian = new GuardianProfile { Id = Guid.NewGuid(), UserId = guardianOwnerUser.Id, User = guardianOwnerUser, Parentesco = "Padre" };

        var studentUser = new ApplicationUser { Id = Guid.NewGuid(), Nombres = "Leo", Apellidos = "Gomez", Email = "leo@tea.edu" };
        var student = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = studentUser.Id,
            User = studentUser,
            CodigoEstudiante = "EST-SEC-03",
            GuardianProfileId = guardian.Id,
            GuardianProfile = guardian
        };

        var category = new LearningCategory { Id = Guid.NewGuid(), Nombre = "Cat", Color = "#2F6E5C", Orden = 1 };
        var module = new LearningModule { Id = Guid.NewGuid(), Titulo = "Mod", Descripcion = "Desc", CategoriaId = category.Id, Categoria = category, Orden = 1 };
        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Titulo = "Rutina",
            Descripcion = "Desc",
            ObjetivoPedagogico = "Obj",
            TipoActividad = TipoActividad.SecuenciaLogica,
            ConfigJson = "{}",
            ModuloId = module.Id,
            Modulo = module,
            DocenteId = teacher1.Id,
            Docente = teacher1
        };

        var assignment = new ActivityAssignment
        {
            Id = Guid.NewGuid(),
            ActivityId = activity.Id,
            Activity = activity,
            StudentProfileId = student.Id,
            StudentProfile = student,
            FechaInicio = DateTime.UtcNow,
            Estado = EstadoAsignacion.Completada,
            OrdenSecuencial = 1
        };

        context.Users.AddRange(teacher1User, teacher2User, guardianOwnerUser, studentUser);
        context.TeacherProfiles.AddRange(teacher1, teacher2);
        context.GuardianProfiles.Add(guardian);
        context.StudentProfiles.Add(student);
        context.LearningCategories.Add(category);
        context.LearningModules.Add(module);
        context.Activities.Add(activity);
        context.ActivityAssignments.Add(assignment);
        await context.SaveChangesAsync();

        var assignmentRepo = new ActivityAssignmentRepository(context);
        var activityRepo = new ActivityRepository(context);
        var studentRepo = new StudentRepository(context);
        var service = new AssignmentService(assignmentRepo, activityRepo, studentRepo, context);

        // 1. Acudiente no vinculado intenta consultar asignaciones del estudiante
        var unlinkedGuardianRes = await service.GetAssignmentsAsync(
            studentProfileId: student.Id,
            classroomId: null,
            estado: null,
            currentUserId: attackerGuardianUserId,
            isGuardianOnly: true);
        Assert.False(unlinkedGuardianRes.Success);
        Assert.Contains("No tiene autorización", unlinkedGuardianRes.Message);

        // 2. Docente 2 (no creador de la actividad) intenta calificar la asignación de Docente 1
        var gradeRes = await service.GradeAssignmentAsync(
            assignment.Id,
            teacher2User.Id,
            new GradeAssignmentDto { Calificacion = 4.5m, Comentario = "Intento IDOR" },
            isAdmin: false);
        Assert.False(gradeRes.Success);
        Assert.Contains("No tiene autorización", gradeRes.Message);
    }

    [Fact]
    public async Task ActivityService_ShouldRejectXssPayloadsInConfigJson()
    {
        using var context = CreateInMemoryContext();

        var category = new LearningCategory { Id = Guid.NewGuid(), Nombre = "Cat", Color = "#2F6E5C", Orden = 1 };
        var module = new LearningModule { Id = Guid.NewGuid(), Titulo = "Mod", Descripcion = "Desc", CategoriaId = category.Id, Orden = 1 };
        context.LearningCategories.Add(category);
        context.LearningModules.Add(module);
        await context.SaveChangesAsync();

        var activityRepo = new ActivityRepository(context);
        var moduleRepo = new LearningModuleRepository(context);
        var service = new ActivityService(activityRepo, moduleRepo, context);

        var dtoWithXss = new CreateActivityDto
        {
            Titulo = "Actividad con XSS",
            Descripcion = "Instrucción literal",
            ObjetivoPedagogico = "Objetivo",
            TipoActividad = TipoActividad.SecuenciaLogica,
            ModuloId = module.Id,
            ConfigJson = "{\"pasos\":[\"<script>alert(1)</script>\"]}"
        };

        var res = await service.CreateActivityAsync(Guid.NewGuid(), dtoWithXss);
        Assert.False(res.Success);
    }

    [Fact]
    public async Task ActivityExecution_ShouldRejectCompletedWithZeroHits()
    {
        using var context = CreateInMemoryContext();

        var studentUser = new ApplicationUser { Id = Guid.NewGuid(), Nombres = "Mateo", Apellidos = "Ruiz", Email = "mateo@tea.edu" };
        var teacherUser = new ApplicationUser { Id = Guid.NewGuid(), Nombres = "Doc", Apellidos = "Tea", Email = "doc@tea.edu" };
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = teacherUser.Id, User = teacherUser, Especialidad = "TEA" };
        var student = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = studentUser.Id,
            User = studentUser,
            CodigoEstudiante = "EST-SEC-04"
        };
        var category = new LearningCategory { Id = Guid.NewGuid(), Nombre = "Cat", Color = "#2F6E5C", Orden = 1 };
        var module = new LearningModule { Id = Guid.NewGuid(), Titulo = "Mod", Descripcion = "Desc", CategoriaId = category.Id, Categoria = category, Orden = 1 };
        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Titulo = "Secuencia",
            Descripcion = "Desc",
            ObjetivoPedagogico = "Obj",
            ConfigJson = "{}",
            ModuloId = module.Id,
            Modulo = module,
            DocenteId = teacher.Id,
            Docente = teacher
        };
        var assignment = new ActivityAssignment
        {
            Id = Guid.NewGuid(),
            ActivityId = activity.Id,
            Activity = activity,
            StudentProfileId = student.Id,
            StudentProfile = student,
            Estado = EstadoAsignacion.EnProgreso
        };

        context.Users.AddRange(studentUser, teacherUser);
        context.TeacherProfiles.Add(teacher);
        context.StudentProfiles.Add(student);
        context.LearningCategories.Add(category);
        context.LearningModules.Add(module);
        context.Activities.Add(activity);
        context.ActivityAssignments.Add(assignment);
        await context.SaveChangesAsync();

        var service = new ActivityExecutionService(
            new ActivityAssignmentRepository(context),
            new ActivityExecutionRepository(context),
            new StudentRepository(context));

        var submitRes = await service.SubmitActivityAsync(
            assignment.Id,
            studentUser.Id,
            new SubmitActivityDto
            {
                TiempoRespuestaSegundos = 10,
                CantidadIntentosFallidos = 0,
                CantidadAciertos = 0,
                CompletadaExitosamente = true
            });

        Assert.False(submitRes.Success);
        Assert.Contains("al menos 1 acierto", submitRes.Message);
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_ShouldMaskInternalError_InProduction()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        RequestDelegate next = _ => throw new InvalidOperationException("SQL Server Secret Connection Error: table AspNetUsers");
        var logger = NullLogger<ExceptionHandlingMiddleware>.Instance;
        var prodEnv = new FakeHostEnvironment { EnvironmentName = Environments.Production };

        var middleware = new ExceptionHandlingMiddleware(next, logger, prodEnv);
        await middleware.InvokeAsync(context);

        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        Assert.DoesNotContain("SQL Server Secret Connection Error", body);
        Assert.Contains("Error interno del servidor", body);
    }

    [Fact]
    public async Task LocalFileStorageService_ShouldBlockPolyglotsAndPathTraversal()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"tea_sec_test_{Guid.NewGuid()}");
        try
        {
            var storage = new LocalFileStorageService(tempRoot);

            // 1. Bloquear SVG/Script disfrazado de .png
            using var svgStream = new MemoryStream(Encoding.UTF8.GetBytes("<svg onload=\"alert(1)\"></svg>"));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                storage.SaveAvatarAsync(Guid.NewGuid(), svgStream, "evil.png", "image/png"));

            // 2. Bloquear Path Traversal en DeleteFileAsync
            Directory.CreateDirectory(tempRoot);
            var sensitiveFile = Path.Combine(tempRoot, "sensitive.txt");
            await File.WriteAllTextAsync(sensitiveFile, "do-not-delete");

            var deleted = await storage.DeleteFileAsync("/uploads/avatars/../../sensitive.txt");
            Assert.False(deleted);
            Assert.True(File.Exists(sensitiveFile));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "TeaLearning.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
