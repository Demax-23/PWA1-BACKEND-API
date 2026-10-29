using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Data.Context;
using TeaLearning.Data.Repositories;
using TeaLearning.Services.Services;

namespace TeaLearning.Tests;

public class ActivityDeletionTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ActivityRepository _activityRepository;
    private readonly LearningModuleRepository _moduleRepository;
    private readonly ActivityService _activityService;

    public ActivityDeletionTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "ActivityDeletionTestDb_" + Guid.NewGuid().ToString("N"))
            .Options;

        _context = new ApplicationDbContext(options);
        _activityRepository = new ActivityRepository(_context);
        _moduleRepository = new LearningModuleRepository(_context);
        _activityService = new ActivityService(_activityRepository, _moduleRepository, _context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private (ApplicationUser User, TeacherProfile Profile) CreateTeacher(string email = "docente@test.com")
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            Nombres = "Profesor",
            Apellidos = "Test"
        };
        var profile = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            Especialidad = "Educación Especial"
        };
        _context.Users.Add(user);
        _context.TeacherProfiles.Add(profile);
        return (user, profile);
    }

    private LearningModule CreateModule()
    {
        var cat = new LearningCategory
        {
            Id = Guid.NewGuid(),
            Nombre = "Categoría Test",
            Orden = 1,
            IsActive = true
        };
        var mod = new LearningModule
        {
            Id = Guid.NewGuid(),
            CategoriaId = cat.Id,
            Categoria = cat,
            Titulo = "Módulo Test",
            Orden = 1,
            IsActive = true
        };
        _context.LearningCategories.Add(cat);
        _context.LearningModules.Add(mod);
        return mod;
    }

    [Fact]
    public async Task DeleteActivityAsync_WithoutAssignments_PermanentlyDeletesActivity()
    {
        // Arrange
        var (teacherUser, teacher) = CreateTeacher();
        var module = CreateModule();

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Titulo = "Actividad a borrar",
            Descripcion = "Descripción test",
            TipoActividad = TipoActividad.SecuenciaLogica,
            ObjetivoPedagogico = "Objetivo test",
            ConfigJson = "{}",
            ModuloId = module.Id,
            Modulo = module,
            DocenteId = teacher.Id,
            Docente = teacher,
            IsActive = true
        };

        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();

        // Act
        var result = await _activityService.DeleteActivityAsync(activity.Id, teacherUser.Id, isAdmin: false);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Actividad pedagógica eliminada exitosamente", result.Message);

        var inDb = await _context.Activities.FindAsync(activity.Id);
        Assert.Null(inDb);
    }

    [Fact]
    public async Task DeleteActivityAsync_WithAssignmentsExecutionsAndNotes_CascadesAndNullifiesNoteAssignment()
    {
        // Arrange
        var (teacherUser, teacher) = CreateTeacher();
        var module = CreateModule();

        var studentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "estudiante@test.com",
            UserName = "estudiante@test.com",
            Nombres = "Estudiante",
            Apellidos = "Test"
        };
        var student = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = studentUser.Id,
            User = studentUser,
            CodigoEstudiante = "EST-001",
            FechaNacimiento = new DateTime(2015, 1, 1)
        };
        _context.Users.Add(studentUser);
        _context.StudentProfiles.Add(student);

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Titulo = "Actividad con asignaciones",
            Descripcion = "Descripción test",
            TipoActividad = TipoActividad.EmparejamientoVisual,
            ObjetivoPedagogico = "Objetivo test",
            ConfigJson = "{}",
            ModuloId = module.Id,
            Modulo = module,
            DocenteId = teacher.Id,
            Docente = teacher,
            IsActive = true
        };
        var assignment = new ActivityAssignment
        {
            Id = Guid.NewGuid(),
            ActivityId = activity.Id,
            Activity = activity,
            StudentProfileId = student.Id,
            StudentProfile = student,
            FechaInicio = DateTime.UtcNow,
            FechaLimite = DateTime.UtcNow.AddDays(7),
            Prioridad = PrioridadActividad.Media,
            Estado = EstadoAsignacion.EnProgreso,
            IsActive = true
        };
        var execution = new ActivityExecution
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignment.Id,
            Assignment = assignment,
            StudentProfileId = student.Id,
            StudentProfile = student,
            FechaRegistro = DateTime.UtcNow,
            TiempoRespuestaSegundos = 60,
            CantidadAciertos = 5,
            CantidadIntentosFallidos = 1,
            CompletadaExitosamente = true,
            IsActive = true
        };
        var note = new PedagogicalNote
        {
            Id = Guid.NewGuid(),
            StudentProfileId = student.Id,
            StudentProfile = student,
            TeacherProfileId = teacher.Id,
            TeacherProfile = teacher,
            Contenido = "Observación vinculada a la asignación",
            AssignmentId = assignment.Id,
            IsActive = true
        };

        _context.Activities.Add(activity);
        _context.ActivityAssignments.Add(assignment);
        _context.ActivityExecutions.Add(execution);
        _context.PedagogicalNotes.Add(note);
        await _context.SaveChangesAsync();

        // Act
        var result = await _activityService.DeleteActivityAsync(activity.Id, teacherUser.Id, isAdmin: false);

        // Assert
        Assert.True(result.Success);

        // Activity is permanently deleted
        Assert.Null(await _context.Activities.FindAsync(activity.Id));

        // Assignment is removed
        Assert.Null(await _context.ActivityAssignments.FindAsync(assignment.Id));

        // Execution is removed
        Assert.Null(await _context.ActivityExecutions.FindAsync(execution.Id));

        // Pedagogical note still exists, but AssignmentId is set to null
        var refreshedNote = await _context.PedagogicalNotes.FindAsync(note.Id);
        Assert.NotNull(refreshedNote);
        Assert.Null(refreshedNote.AssignmentId);
    }

    [Fact]
    public async Task DeleteActivityAsync_WhenTeacherDoesNotOwnActivity_ReturnsForbiddenError()
    {
        // Arrange
        var (_, ownerTeacher) = CreateTeacher("owner@test.com");
        var (otherTeacherUser, _) = CreateTeacher("other@test.com");
        var module = CreateModule();

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Titulo = "Actividad de otro docente",
            Descripcion = "Descripción test",
            TipoActividad = TipoActividad.SecuenciaLogica,
            ObjetivoPedagogico = "Objetivo test",
            ConfigJson = "{}",
            ModuloId = module.Id,
            Modulo = module,
            DocenteId = ownerTeacher.Id,
            Docente = ownerTeacher,
            IsActive = true
        };

        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();

        // Act: Another teacher tries to delete it
        var result = await _activityService.DeleteActivityAsync(activity.Id, otherTeacherUser.Id, isAdmin: false);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("permisos para eliminar", result.Message);

        // Activity still exists
        Assert.NotNull(await _context.Activities.FindAsync(activity.Id));
    }

    [Fact]
    public async Task DeleteActivityAsync_WhenAdminDeletesAnotherTeachersActivity_Succeeds()
    {
        // Arrange
        var (_, ownerTeacher) = CreateTeacher("owner2@test.com");
        var module = CreateModule();
        var adminUserId = Guid.NewGuid();

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Titulo = "Actividad docente borrada por admin",
            Descripcion = "Descripción test",
            TipoActividad = TipoActividad.SecuenciaLogica,
            ObjetivoPedagogico = "Objetivo test",
            ConfigJson = "{}",
            ModuloId = module.Id,
            Modulo = module,
            DocenteId = ownerTeacher.Id,
            Docente = ownerTeacher,
            IsActive = true
        };

        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();

        // Act: Admin deletes it
        var result = await _activityService.DeleteActivityAsync(activity.Id, adminUserId, isAdmin: true);

        // Assert
        Assert.True(result.Success);
        Assert.Null(await _context.Activities.FindAsync(activity.Id));
    }
}
