using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Data.Context;
using TeaLearning.Data.Repositories;
using TeaLearning.Services.Services;

namespace TeaLearning.Tests;

public class TeacherAnalyticsServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly TeacherAnalyticsService _service;

    public TeacherAnalyticsServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "TeacherAnalyticsTestDb_" + Guid.NewGuid().ToString("N"))
            .Options;

        _context = new ApplicationDbContext(options);

        var studentRepo = new StudentRepository(_context);
        var execRepo = new ActivityExecutionRepository(_context);
        var assignRepo = new ActivityAssignmentRepository(_context);

        _service = new TeacherAnalyticsService(studentRepo, execRepo, assignRepo, _context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task GetClassroomAnalyticsSummaryAsync_ShouldCalculateFailedAttemptsAndWeeklyEvolution()
    {
        // Arrange: Create Classroom and Student
        var teacherUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Nombres = "Laura",
            Apellidos = "Gómez",
            Email = "laura@tealearning.com",
            UserName = "laura@tealearning.com"
        };
        var teacherProfile = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = teacherUser.Id,
            User = teacherUser,
            Especialidad = "Educación Especial"
        };

        var classroom = new Classroom
        {
            Id = Guid.NewGuid(),
            Nombre = "Aula Azul",
            Grado = "Transición",
            TeacherProfileId = teacherProfile.Id,
            TeacherProfile = teacherProfile,
            IsActive = true
        };

        var studentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Nombres = "Mariana",
            Apellidos = "Vargas García",
            Email = "mariana@tealearning.com",
            UserName = "mariana@tealearning.com"
        };
        var studentProfile = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = studentUser.Id,
            User = studentUser,
            CodigoEstudiante = "EST-001",
            ClassroomId = classroom.Id,
            Classroom = classroom,
            IsActive = true
        };

        var category = new LearningCategory { Id = Guid.NewGuid(), Nombre = "Atención", IsActive = true };
        var module = new LearningModule { Id = Guid.NewGuid(), Titulo = "Módulo 1", CategoriaId = category.Id, IsActive = true };
        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Titulo = "Empareja figuras",
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
            Estado = EstadoAsignacion.Completada,
            IsActive = true
        };

        var mondayWeek1 = new DateTime(2026, 9, 7, 10, 0, 0, DateTimeKind.Utc);
        var mondayWeek2 = new DateTime(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc);

        var exec1 = new ActivityExecution
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignment.Id,
            Assignment = assignment,
            StudentProfileId = studentProfile.Id,
            StudentProfile = studentProfile,
            CantidadAciertos = 4,
            CantidadIntentosFallidos = 2,
            TiempoRespuestaSegundos = 45,
            CompletadaExitosamente = true,
            FechaRegistro = mondayWeek1,
            IsActive = true
        };

        var exec2 = new ActivityExecution
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignment.Id,
            Assignment = assignment,
            StudentProfileId = studentProfile.Id,
            StudentProfile = studentProfile,
            CantidadAciertos = 5,
            CantidadIntentosFallidos = 1,
            TiempoRespuestaSegundos = 35,
            CompletadaExitosamente = true,
            FechaRegistro = mondayWeek2,
            IsActive = true
        };

        _context.Users.AddRange(teacherUser, studentUser);
        _context.TeacherProfiles.Add(teacherProfile);
        _context.Classrooms.Add(classroom);
        _context.StudentProfiles.Add(studentProfile);
        _context.LearningCategories.Add(category);
        _context.LearningModules.Add(module);
        _context.Activities.Add(activity);
        _context.ActivityAssignments.Add(assignment);
        _context.ActivityExecutions.AddRange(exec1, exec2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetClassroomAnalyticsSummaryAsync(classroom.Id);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var classDto = result.Data;
        Assert.Equal(2, classDto.TotalIntentosAula);
        Assert.Equal(1.5, classDto.PromedioIntentosFallidosAula); // (2 + 1) / 2 = 1.5

        Assert.Single(classDto.DesempenoEstudiantes);
        var studentPerf = classDto.DesempenoEstudiantes[0];
        Assert.Equal("Mariana Vargas García", studentPerf.NombreCompleto);
        Assert.Equal(2, studentPerf.TotalIntentos);
        Assert.Equal(2, studentPerf.ActividadesCompletadas);
        Assert.Equal(1.5, studentPerf.IntentosFallidosPromedio); // 3 fallos / 2 intentos = 1.5

        // Weekly evolution must have 2 points matching Mondays
        Assert.Equal(2, studentPerf.EvolucionSemanal.Count);
        Assert.Equal(mondayWeek1.Date, studentPerf.EvolucionSemanal[0].FechaInicioSemana.Date);
        Assert.Equal(mondayWeek2.Date, studentPerf.EvolucionSemanal[1].FechaInicioSemana.Date);
    }
}
