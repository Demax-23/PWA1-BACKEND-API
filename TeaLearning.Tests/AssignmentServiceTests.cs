using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Services.DTOs.Assignments;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Tests;

public class AssignmentServiceTests : IDisposable
{
    private readonly AppTestHost _host = new();
    private IAssignmentService Svc => _host.Get<IAssignmentService>();

    public void Dispose() => _host.Dispose();

    private record World(
        ApplicationUser TeacherUser, TeacherProfile Teacher,
        ApplicationUser GuardianUser, GuardianProfile Guardian,
        Classroom Classroom, ApplicationUser StudentUser, StudentProfile Student, Activity Activity);

    private async Task<World> SeedAsync()
    {
        var (tu, t) = await _host.CreateTeacherAsync();
        var (gu, g) = await _host.CreateGuardianAsync();
        var classroom = await _host.CreateClassroomAsync(t);
        var (su, s) = await _host.CreateStudentAsync(classroom, g);
        var module = await _host.CreateModuleAsync();
        var activity = await _host.CreateActivityAsync(module, t);
        return new World(tu, t, gu, g, classroom, su, s, activity);
    }

    // ------------------------------------------------------------ crear

    [Fact]
    public async Task Create_ActividadInexistente_Falla()
    {
        var result = await Svc.CreateAssignmentAsync(new CreateAssignmentDto { ActivityId = Guid.NewGuid(), StudentProfileId = Guid.NewGuid() });

        Assert.False(result.Success);
        Assert.Contains("no existe", result.Message);
    }

    [Fact]
    public async Task Create_SinEstudianteNiAula_Falla()
    {
        var w = await SeedAsync();

        var result = await Svc.CreateAssignmentAsync(new CreateAssignmentDto { ActivityId = w.Activity.Id });

        Assert.False(result.Success);
        Assert.Contains("StudentProfileId", result.Message);
    }

    [Fact]
    public async Task Create_EstudianteInexistente_Falla()
    {
        var w = await SeedAsync();

        var result = await Svc.CreateAssignmentAsync(new CreateAssignmentDto { ActivityId = w.Activity.Id, StudentProfileId = Guid.NewGuid() });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Create_Individual_CreaAsignacionPendiente()
    {
        var w = await SeedAsync();

        var result = await Svc.CreateAssignmentAsync(new CreateAssignmentDto
        {
            ActivityId = w.Activity.Id,
            StudentProfileId = w.Student.Id,
            OrdenSecuencial = 3,
            Prioridad = PrioridadActividad.Alta,
            FechaLimite = DateTime.UtcNow.AddDays(5)
        });

        Assert.True(result.Success);
        var dto = Assert.Single(result.Data!);
        Assert.Equal(EstadoAsignacion.Pendiente, dto.Estado);
        Assert.Equal(PrioridadActividad.Alta, dto.Prioridad);
        Assert.Equal(3, dto.OrdenSecuencial);
        Assert.Equal(w.Student.CodigoEstudiante, dto.CodigoEstudiante);
    }

    [Fact]
    public async Task Create_IndividualExistente_ActualizaEnLugarDeDuplicar()
    {
        var w = await SeedAsync();
        await _host.CreateAssignmentAsync(w.Activity, w.Student);
        var limite = DateTime.UtcNow.AddDays(9);

        var result = await Svc.CreateAssignmentAsync(new CreateAssignmentDto
        {
            ActivityId = w.Activity.Id,
            StudentProfileId = w.Student.Id,
            OrdenSecuencial = 7,
            Prioridad = PrioridadActividad.Baja,
            FechaLimite = limite
        });

        Assert.True(result.Success);
        Assert.Equal(1, await _host.Context.ActivityAssignments.CountAsync());
        Assert.Equal(7, result.Data![0].OrdenSecuencial);
        Assert.Equal(limite, result.Data[0].FechaLimite);
    }

    [Fact]
    public async Task Create_IndividualExistenteSinFechaLimite_ConservaLaAnterior()
    {
        var w = await SeedAsync();
        var existing = await _host.CreateAssignmentAsync(w.Activity, w.Student);
        var original = existing.FechaLimite;

        var result = await Svc.CreateAssignmentAsync(new CreateAssignmentDto { ActivityId = w.Activity.Id, StudentProfileId = w.Student.Id });

        Assert.Equal(original, result.Data![0].FechaLimite);
    }

    [Fact]
    public async Task Create_AulaInexistente_Falla()
    {
        var w = await SeedAsync();

        var result = await Svc.CreateAssignmentAsync(new CreateAssignmentDto { ActivityId = w.Activity.Id, ClassroomId = Guid.NewGuid() });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Create_AulaSinEstudiantesActivos_Falla()
    {
        var w = await SeedAsync();
        var empty = await _host.CreateClassroomAsync(w.Teacher, "Vacía");

        var result = await Svc.CreateAssignmentAsync(new CreateAssignmentDto { ActivityId = w.Activity.Id, ClassroomId = empty.Id });

        Assert.False(result.Success);
        Assert.Contains("no cuenta con estudiantes", result.Message);
    }

    [Fact]
    public async Task Create_PorAula_AsignaATodosYActualizaLasExistentes()
    {
        var w = await SeedAsync();
        var (_, second) = await _host.CreateStudentAsync(w.Classroom);
        await _host.CreateAssignmentAsync(w.Activity, w.Student);

        var result = await Svc.CreateAssignmentAsync(new CreateAssignmentDto
        {
            ActivityId = w.Activity.Id,
            ClassroomId = w.Classroom.Id,
            FechaLimite = DateTime.UtcNow.AddDays(3),
            OrdenSecuencial = 2
        });

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count);
        Assert.Equal(2, await _host.Context.ActivityAssignments.CountAsync());
        Assert.NotEqual(Guid.Empty, second.Id);
    }

    [Fact]
    public async Task Create_PorAulaDondeTodosYaTienenLaActividad_SoloActualiza()
    {
        var w = await SeedAsync();
        await _host.CreateAssignmentAsync(w.Activity, w.Student);

        var result = await Svc.CreateAssignmentAsync(new CreateAssignmentDto { ActivityId = w.Activity.Id, ClassroomId = w.Classroom.Id });

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal(1, await _host.Context.ActivityAssignments.CountAsync());
    }

    // ------------------------------------------------------------ consultas

    [Fact]
    public async Task GetAssignments_SinFiltros_DevuelveTodas()
    {
        var w = await SeedAsync();
        await _host.CreateAssignmentAsync(w.Activity, w.Student);

        var result = await Svc.GetAssignmentsAsync();

        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task GetAssignments_FiltraPorEstudianteAulaYEstado()
    {
        var w = await SeedAsync();
        await _host.CreateAssignmentAsync(w.Activity, w.Student, EstadoAsignacion.Completada);

        Assert.Single((await Svc.GetAssignmentsAsync(studentProfileId: w.Student.Id)).Data!);
        Assert.Single((await Svc.GetAssignmentsAsync(classroomId: w.Classroom.Id)).Data!);
        Assert.Single((await Svc.GetAssignmentsAsync(estado: EstadoAsignacion.Completada)).Data!);
        Assert.Empty((await Svc.GetAssignmentsAsync(estado: EstadoAsignacion.EnProgreso)).Data!);
    }

    [Fact]
    public async Task GetAssignments_Acudiente_SoloVeLasDeSusEstudiantes()
    {
        var w = await SeedAsync();
        await _host.CreateAssignmentAsync(w.Activity, w.Student);
        var (_, other) = await _host.CreateStudentAsync();
        await _host.CreateAssignmentAsync(w.Activity, other);

        var result = await Svc.GetAssignmentsAsync(currentUserId: w.GuardianUser.Id, isGuardianOnly: true);

        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task GetAssignments_AcudienteConEstudianteAjeno_Falla()
    {
        var w = await SeedAsync();
        var (_, other) = await _host.CreateStudentAsync();

        var result = await Svc.GetAssignmentsAsync(studentProfileId: other.Id, currentUserId: w.GuardianUser.Id, isGuardianOnly: true);

        Assert.False(result.Success);
        Assert.Contains("autorización", result.Message);
    }

    [Fact]
    public async Task GetAssignments_AcudienteConEstudianteInexistente_Falla()
    {
        var w = await SeedAsync();

        var result = await Svc.GetAssignmentsAsync(studentProfileId: Guid.NewGuid(), currentUserId: w.GuardianUser.Id, isGuardianOnly: true);

        Assert.False(result.Success);
        Assert.Contains("no existe", result.Message);
    }

    [Fact]
    public async Task GetAssignments_AcudienteConSuEstudiante_Funciona()
    {
        var w = await SeedAsync();
        await _host.CreateAssignmentAsync(w.Activity, w.Student);

        var result = await Svc.GetAssignmentsAsync(studentProfileId: w.Student.Id, currentUserId: w.GuardianUser.Id, isGuardianOnly: true);

        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task GetById_Inexistente_Falla()
    {
        Assert.False((await Svc.GetAssignmentByIdAsync(Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task GetById_Staff_Funciona()
    {
        var w = await SeedAsync();
        var a = await _host.CreateAssignmentAsync(w.Activity, w.Student);

        var result = await Svc.GetAssignmentByIdAsync(a.Id);

        Assert.True(result.Success);
        Assert.Equal(w.Activity.Titulo, result.Data!.ActivityTitulo);
    }

    [Fact]
    public async Task GetById_NoStaff_EstudiantePropioYAcudienteVinculadoPuedenVer_OtrosNo()
    {
        var w = await SeedAsync();
        var a = await _host.CreateAssignmentAsync(w.Activity, w.Student);

        Assert.True((await Svc.GetAssignmentByIdAsync(a.Id, w.StudentUser.Id, isStaff: false)).Success);
        Assert.True((await Svc.GetAssignmentByIdAsync(a.Id, w.GuardianUser.Id, isStaff: false)).Success);
        var denied = await Svc.GetAssignmentByIdAsync(a.Id, Guid.NewGuid(), isStaff: false);
        Assert.False(denied.Success);
        Assert.Contains("autorización", denied.Message);
    }

    // ------------------------------------------------------------ eliminar

    [Fact]
    public async Task Delete_Inexistente_Falla()
    {
        Assert.False((await Svc.DeleteAssignmentAsync(Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task Delete_Admin_Elimina()
    {
        var w = await SeedAsync();
        var a = await _host.CreateAssignmentAsync(w.Activity, w.Student);

        var result = await Svc.DeleteAssignmentAsync(a.Id, null, isAdmin: true);

        Assert.True(result.Success);
        Assert.False((await _host.Context.ActivityAssignments.AsNoTracking().SingleAsync()).IsActive); // baja lógica
    }

    [Fact]
    public async Task Delete_UsuarioSinPerfilDocente_Falla()
    {
        var w = await SeedAsync();
        var a = await _host.CreateAssignmentAsync(w.Activity, w.Student);

        var result = await Svc.DeleteAssignmentAsync(a.Id, Guid.NewGuid(), isAdmin: false);

        Assert.False(result.Success);
        Assert.Contains("perfil docente", result.Message);
    }

    [Fact]
    public async Task Delete_DocenteAjeno_Falla_YDuenoDeLaActividadPuede()
    {
        var w = await SeedAsync();
        var a = await _host.CreateAssignmentAsync(w.Activity, w.Student);
        var (otherUser, _) = await _host.CreateTeacherAsync();

        var denied = await Svc.DeleteAssignmentAsync(a.Id, otherUser.Id, isAdmin: false);
        var allowed = await Svc.DeleteAssignmentAsync(a.Id, w.TeacherUser.Id, isAdmin: false);

        Assert.False(denied.Success);
        Assert.Contains("otro docente", denied.Message);
        Assert.True(allowed.Success);
    }

    [Fact]
    public async Task Delete_DocenteTitularDelAulaPuedeAunqueNoSeaDuenoDeLaActividad()
    {
        var (_, activityOwner) = await _host.CreateTeacherAsync();
        var (classroomTeacherUser, classroomTeacher) = await _host.CreateTeacherAsync();
        var classroom = await _host.CreateClassroomAsync(classroomTeacher);
        var (_, student) = await _host.CreateStudentAsync(classroom);
        var activity = await _host.CreateActivityAsync(await _host.CreateModuleAsync(), activityOwner);
        var a = await _host.CreateAssignmentAsync(activity, student);

        var result = await Svc.DeleteAssignmentAsync(a.Id, classroomTeacherUser.Id, isAdmin: false);

        Assert.True(result.Success);
    }

    // ------------------------------------------------------------ calificar

    [Fact]
    public async Task Grade_Inexistente_Falla()
    {
        Assert.False((await Svc.GradeAssignmentAsync(Guid.NewGuid(), Guid.NewGuid(), new GradeAssignmentDto { Calificacion = 4 })).Success);
    }

    [Fact]
    public async Task Grade_SinPerfilDocenteYNoAdmin_Falla()
    {
        var w = await SeedAsync();
        var a = await _host.CreateAssignmentAsync(w.Activity, w.Student);

        var result = await Svc.GradeAssignmentAsync(a.Id, Guid.NewGuid(), new GradeAssignmentDto { Calificacion = 4 });

        Assert.False(result.Success);
        Assert.Contains("perfil docente", result.Message);
    }

    [Fact]
    public async Task Grade_DocenteAjeno_Falla()
    {
        var w = await SeedAsync();
        var a = await _host.CreateAssignmentAsync(w.Activity, w.Student);
        var (otherUser, _) = await _host.CreateTeacherAsync();

        var result = await Svc.GradeAssignmentAsync(a.Id, otherUser.Id, new GradeAssignmentDto { Calificacion = 4 });

        Assert.False(result.Success);
        Assert.Contains("otro docente", result.Message);
    }

    [Fact]
    public async Task Grade_Docente_RedondeaYGuardaComentario()
    {
        var w = await SeedAsync();
        var a = await _host.CreateAssignmentAsync(w.Activity, w.Student);

        var result = await Svc.GradeAssignmentAsync(a.Id, w.TeacherUser.Id, new GradeAssignmentDto { Calificacion = 4.26m, Comentario = "  Muy bien  " });

        Assert.True(result.Success);
        Assert.Equal(4.3m, result.Data!.Calificacion);
        Assert.Equal("Muy bien", result.Data.Comentario);
        Assert.Equal(w.TeacherUser.NombreCompleto, result.Data.DocenteNombre);
    }

    [Fact]
    public async Task Grade_ComentarioEnBlanco_SeGuardaNulo()
    {
        var w = await SeedAsync();
        var a = await _host.CreateAssignmentAsync(w.Activity, w.Student);

        var result = await Svc.GradeAssignmentAsync(a.Id, w.TeacherUser.Id, new GradeAssignmentDto { Calificacion = 3, Comentario = "   " });

        Assert.Null(result.Data!.Comentario);
    }

    [Fact]
    public async Task Grade_AdminSinPerfilDocente_CalificaYLimpiaDocente()
    {
        var w = await SeedAsync();
        var a = await _host.CreateAssignmentAsync(w.Activity, w.Student);
        var admin = await _host.CreateAdminAsync();

        var result = await Svc.GradeAssignmentAsync(a.Id, admin.Id, new GradeAssignmentDto { Calificacion = 5 }, isAdmin: true);

        Assert.True(result.Success);
        Assert.Equal(admin.NombreCompleto, result.Data!.DocenteNombre);
        Assert.Null((await _host.Context.ActivityAssignments.AsNoTracking().SingleAsync()).CalificadoPorDocenteId);
    }

    [Fact]
    public async Task Grade_AdminConPerfilDocente_RegistraAlDocente()
    {
        var w = await SeedAsync();
        var a = await _host.CreateAssignmentAsync(w.Activity, w.Student);

        var result = await Svc.GradeAssignmentAsync(a.Id, w.TeacherUser.Id, new GradeAssignmentDto { Calificacion = 2 }, isAdmin: true);

        Assert.True(result.Success);
        Assert.Equal(w.Teacher.Id, (await _host.Context.ActivityAssignments.AsNoTracking().SingleAsync()).CalificadoPorDocenteId);
    }

    // ------------------------------------------------------------ quitar calificación

    [Fact]
    public async Task RemoveGrade_Inexistente_Falla()
    {
        Assert.False((await Svc.RemoveGradeAsync(Guid.NewGuid(), Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task RemoveGrade_SinPerfilDocente_Falla()
    {
        var w = await SeedAsync();
        var a = await _host.CreateAssignmentAsync(w.Activity, w.Student);

        Assert.False((await Svc.RemoveGradeAsync(a.Id, Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task RemoveGrade_DocenteAjeno_Falla()
    {
        var w = await SeedAsync();
        var a = await _host.CreateAssignmentAsync(w.Activity, w.Student);
        var (otherUser, _) = await _host.CreateTeacherAsync();

        var result = await Svc.RemoveGradeAsync(a.Id, otherUser.Id);

        Assert.False(result.Success);
        Assert.Contains("otro usuario", result.Message);
    }

    [Fact]
    public async Task RemoveGrade_DocenteCalificador_LimpiaLaNota()
    {
        var w = await SeedAsync();
        var a = await _host.CreateAssignmentAsync(w.Activity, w.Student);
        await Svc.GradeAssignmentAsync(a.Id, w.TeacherUser.Id, new GradeAssignmentDto { Calificacion = 4, Comentario = "ok" });

        var result = await Svc.RemoveGradeAsync(a.Id, w.TeacherUser.Id);

        Assert.True(result.Success);
        var stored = await _host.Context.ActivityAssignments.AsNoTracking().SingleAsync();
        Assert.Null(stored.Calificacion);
        Assert.Null(stored.ComentarioCalificacion);
    }

    [Fact]
    public async Task RemoveGrade_Admin_PuedeSinSerDocente()
    {
        var w = await SeedAsync();
        var a = await _host.CreateAssignmentAsync(w.Activity, w.Student);

        Assert.True((await Svc.RemoveGradeAsync(a.Id, Guid.NewGuid(), isAdmin: true)).Success);
    }

    [Fact]
    public async Task RemoveGrade_TitularDelAulaPuede()
    {
        var (_, owner) = await _host.CreateTeacherAsync();
        var (tu, t) = await _host.CreateTeacherAsync();
        var classroom = await _host.CreateClassroomAsync(t);
        var (_, student) = await _host.CreateStudentAsync(classroom);
        var activity = await _host.CreateActivityAsync(await _host.CreateModuleAsync(), owner);
        var a = await _host.CreateAssignmentAsync(activity, student);

        Assert.True((await Svc.RemoveGradeAsync(a.Id, tu.Id)).Success);
    }

    // ------------------------------------------------------------ calificaciones del estudiante

    [Fact]
    public async Task GetStudentGrades_EstudianteInexistente_Falla()
    {
        Assert.False((await Svc.GetStudentGradesAsync(Guid.NewGuid(), Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task GetStudentGrades_IncluyeUltimoIntentoYTotal()
    {
        var w = await SeedAsync();
        var graded = await _host.CreateAssignmentAsync(w.Activity, w.Student);
        await Svc.GradeAssignmentAsync(graded.Id, w.TeacherUser.Id, new GradeAssignmentDto { Calificacion = 4 });
        _host.Context.ActivityExecutions.Add(new ActivityExecution
        {
            AssignmentId = graded.Id,
            StudentProfileId = w.Student.Id,
            CantidadAciertos = 3,
            CompletadaExitosamente = true,
            FechaRegistro = DateTime.UtcNow.AddMinutes(-5)
        });
        _host.Context.ActivityExecutions.Add(new ActivityExecution
        {
            AssignmentId = graded.Id,
            StudentProfileId = w.Student.Id,
            CantidadAciertos = 5,
            CompletadaExitosamente = true,
            FechaRegistro = DateTime.UtcNow
        });
        await _host.Context.SaveChangesAsync();
        var second = await _host.CreateActivityAsync(await _host.CreateModuleAsync(name: "M2"), w.Teacher, "Otra");
        await _host.CreateAssignmentAsync(second, w.Student, orden: 2);

        var result = await Svc.GetStudentGradesAsync(w.Student.Id, w.GuardianUser.Id);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count);
        var withAttempts = result.Data.Single(g => g.TotalIntentos == 2);
        Assert.Equal(5, withAttempts.UltimoIntento!.CantidadAciertos);
        Assert.Contains(result.Data, g => g.UltimoIntento == null);
    }
}
