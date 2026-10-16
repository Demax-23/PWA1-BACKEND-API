using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Data.Context;
using TeaLearning.Data.Repositories;
using TeaLearning.Services.DTOs.Assignments;
using TeaLearning.Services.Services;

namespace TeaLearning.Tests;

public class AssignmentGradingTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GradeAssignmentAsync_ShouldUpdateGradeAndFeedback()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var assignmentRepo = new ActivityAssignmentRepository(context);
        var activityRepo = new ActivityRepository(context);
        var studentRepo = new StudentRepository(context);
        var service = new AssignmentService(assignmentRepo, activityRepo, studentRepo, context);

        var teacherUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Nombres = "Marta",
            Apellidos = "Ruiz",
            Email = "marta.ruiz@tealearning.com",
            UserName = "marta.ruiz@tealearning.com"
        };
        var teacherProfile = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = teacherUser.Id,
            User = teacherUser,
            Especialidad = "Educación Especial"
        };

        var studentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Nombres = "Mateo",
            Apellidos = "González",
            Email = "mateo@test.com",
            UserName = "mateo@test.com"
        };
        var studentProfile = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = studentUser.Id,
            User = studentUser,
            CodigoEstudiante = "EST-001"
        };

        var module = new LearningModule
        {
            Id = Guid.NewGuid(),
            Titulo = "Rutinas Diarias",
            Descripcion = "Módulo de rutinas",
            IsActive = true
        };

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Titulo = "Ordena tu rutina",
            TipoActividad = TipoActividad.SecuenciaLogica,
            ModuloId = module.Id,
            Modulo = module,
            DocenteId = teacherProfile.Id,
            Docente = teacherProfile,
            IsActive = true
        };

        var assignment = new ActivityAssignment
        {
            Id = Guid.NewGuid(),
            ActivityId = activity.Id,
            Activity = activity,
            StudentProfileId = studentProfile.Id,
            StudentProfile = studentProfile,
            Estado = EstadoAsignacion.Completada
        };

        context.Users.AddRange(teacherUser, studentUser);
        context.TeacherProfiles.Add(teacherProfile);
        context.StudentProfiles.Add(studentProfile);
        context.LearningModules.Add(module);
        context.Activities.Add(activity);
        context.ActivityAssignments.Add(assignment);
        await context.SaveChangesAsync();

        var gradeDto = new GradeAssignmentDto
        {
            Calificacion = 4.5m,
            Comentario = "Excelente desarrollo de la secuencia."
        };

        // Act
        var result = await service.GradeAssignmentAsync(assignment.Id, teacherUser.Id, gradeDto);

        // Assert
        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(4.5m, result.Data.Calificacion);
        Assert.Equal("Excelente desarrollo de la secuencia.", result.Data.Comentario);
        Assert.Equal("Marta Ruiz", result.Data.DocenteNombre);
        Assert.NotNull(result.Data.FechaCalificacion);

        // Verify in DB
        var updated = await context.ActivityAssignments.FindAsync(assignment.Id);
        Assert.NotNull(updated);
        Assert.Equal(4.5m, updated.Calificacion);
        Assert.Equal("Excelente desarrollo de la secuencia.", updated.ComentarioCalificacion);
        Assert.Equal(teacherProfile.Id, updated.CalificadoPorDocenteId);
    }

    [Fact]
    public async Task RemoveGradeAsync_ShouldClearGradeFields()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var assignmentRepo = new ActivityAssignmentRepository(context);
        var activityRepo = new ActivityRepository(context);
        var studentRepo = new StudentRepository(context);
        var service = new AssignmentService(assignmentRepo, activityRepo, studentRepo, context);

        var assignment = new ActivityAssignment
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            StudentProfileId = Guid.NewGuid(),
            Calificacion = 4.0m,
            ComentarioCalificacion = "Buen trabajo",
            FechaCalificacion = DateTime.UtcNow,
            CalificadoPorDocenteId = Guid.NewGuid()
        };
        context.ActivityAssignments.Add(assignment);
        await context.SaveChangesAsync();

        // Act
        var result = await service.RemoveGradeAsync(assignment.Id, Guid.NewGuid());

        // Assert
        Assert.True(result.Success);
        var updated = await context.ActivityAssignments.FindAsync(assignment.Id);
        Assert.NotNull(updated);
        Assert.Null(updated.Calificacion);
        Assert.Null(updated.ComentarioCalificacion);
        Assert.Null(updated.FechaCalificacion);
        Assert.Null(updated.CalificadoPorDocenteId);
    }

    [Fact]
    public async Task GetStudentGradesAsync_ShouldReturnAssignmentsWithLastAttempt()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var assignmentRepo = new ActivityAssignmentRepository(context);
        var activityRepo = new ActivityRepository(context);
        var studentRepo = new StudentRepository(context);
        var service = new AssignmentService(assignmentRepo, activityRepo, studentRepo, context);

        var studentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Nombres = "Lucas",
            Apellidos = "Mora",
            Email = "lucas@test.com",
            UserName = "lucas@test.com"
        };
        var studentProfile = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = studentUser.Id,
            User = studentUser,
            CodigoEstudiante = "EST-002",
            IsActive = true
        };
        var module = new LearningModule
        {
            Id = Guid.NewGuid(),
            Titulo = "Emociones",
            Descripcion = "Módulo de emociones",
            IsActive = true
        };
        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Titulo = "Empareja emociones",
            TipoActividad = TipoActividad.EmparejamientoVisual,
            ModuloId = module.Id,
            Modulo = module,
            IsActive = true
        };
        var assignment = new ActivityAssignment
        {
            Id = Guid.NewGuid(),
            ActivityId = activity.Id,
            Activity = activity,
            StudentProfileId = studentProfile.Id,
            StudentProfile = studentProfile,
            Calificacion = 3.5m,
            ComentarioCalificacion = "Necesita apoyo visual",
            IsActive = true
        };
        var execution1 = new ActivityExecution
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignment.Id,
            StudentProfileId = studentProfile.Id,
            CantidadAciertos = 2,
            CantidadIntentosFallidos = 3,
            CompletadaExitosamente = false,
            TiempoRespuestaSegundos = 60,
            FechaRegistro = DateTime.UtcNow.AddMinutes(-10),
            IsActive = true
        };
        var execution2 = new ActivityExecution
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignment.Id,
            StudentProfileId = studentProfile.Id,
            CantidadAciertos = 5,
            CantidadIntentosFallidos = 1,
            CompletadaExitosamente = true,
            TiempoRespuestaSegundos = 45,
            FechaRegistro = DateTime.UtcNow,
            IsActive = true
        };

        context.Users.Add(studentUser);
        context.StudentProfiles.Add(studentProfile);
        context.LearningModules.Add(module);
        context.Activities.Add(activity);
        context.ActivityAssignments.Add(assignment);
        context.ActivityExecutions.AddRange(execution1, execution2);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetStudentGradesAsync(studentProfile.Id, Guid.NewGuid());

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);

        var gradeItem = result.Data[0];
        Assert.Equal(assignment.Id, gradeItem.AssignmentId);
        Assert.Equal("Empareja emociones", gradeItem.Titulo);
        Assert.Equal(3.5m, gradeItem.Calificacion);
        Assert.Equal(2, gradeItem.TotalIntentos);
        Assert.NotNull(gradeItem.UltimoIntento);
        Assert.Equal(5, gradeItem.UltimoIntento.CantidadAciertos);
        Assert.Equal(1, gradeItem.UltimoIntento.CantidadIntentosFallidos);
        Assert.True(gradeItem.UltimoIntento.CompletadaExitosamente);
        Assert.Equal(45, gradeItem.UltimoIntento.TiempoRespuestaSegundos);
    }
}
