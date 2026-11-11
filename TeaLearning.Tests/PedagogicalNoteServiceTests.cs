using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Services.DTOs.Pedagogical;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Tests;

public class PedagogicalNoteServiceTests : IDisposable
{
    private readonly AppTestHost _host = new();
    private IPedagogicalNoteService Notes => _host.Get<IPedagogicalNoteService>();

    public void Dispose() => _host.Dispose();

    private static CreatePedagogicalNoteDto Dto(string contenido = "  Observación  ") => new() { Contenido = contenido };

    private async Task<(ApplicationUser TeacherUser, TeacherProfile Teacher, ApplicationUser GuardianUser, GuardianProfile Guardian, StudentProfile Student)> SeedAsync()
    {
        var (tu, t) = await _host.CreateTeacherAsync();
        var (gu, g) = await _host.CreateGuardianAsync();
        var (_, s) = await _host.CreateStudentAsync(guardian: g);
        return (tu, t, gu, g, s);
    }

    // ------------------------------------------------------------ crear

    [Fact]
    public async Task Create_EstudianteInexistente_Falla()
    {
        var result = await Notes.CreateNoteAsync(Guid.NewGuid(), Guid.NewGuid(), Dto());

        Assert.False(result.Success);
        Assert.Contains("no encontrado", result.Message);
    }

    [Fact]
    public async Task Create_UsuarioSinPerfilDocenteNiExistente_Falla()
    {
        var (_, _, _, _, student) = await SeedAsync();

        var result = await Notes.CreateNoteAsync(student.Id, Guid.NewGuid(), Dto());

        Assert.False(result.Success);
        Assert.Contains("no encontrado", result.Message);
    }

    [Fact]
    public async Task Create_AdminSinPerfilDocente_CreaPerfilAutomaticamente()
    {
        var (_, _, _, _, student) = await SeedAsync();
        var admin = await _host.CreateAdminAsync();

        var result = await Notes.CreateNoteAsync(student.Id, admin.Id, Dto());

        Assert.True(result.Success);
        Assert.True(await _host.Context.TeacherProfiles.AnyAsync(t => t.UserId == admin.Id));
        Assert.Equal(admin.NombreCompleto, result.Data!.TeacherNombre);
    }

    [Fact]
    public async Task Create_ValoresPorDefectoYRecorte()
    {
        var (tu, _, _, _, student) = await SeedAsync();

        var result = await Notes.CreateNoteAsync(student.Id, tu.Id, Dto());

        Assert.True(result.Success);
        Assert.Equal("Observación", result.Data!.Contenido);
        Assert.Equal("Observacion", result.Data.Tipo);
        Assert.Equal("Socioemocional", result.Data.Area);
        Assert.True(result.Data.VisibleAcudiente);
        Assert.Equal(string.Empty, result.Data.Titulo);
    }

    [Fact]
    public async Task Create_ConTodosLosCampos_Persiste()
    {
        var (tu, _, _, _, student) = await SeedAsync();
        var dto = new CreatePedagogicalNoteDto
        {
            Contenido = "Contenido",
            Titulo = " Título ",
            Tipo = " Logro ",
            Area = " Académica ",
            RecomendacionCasa = " Leer ",
            VisibleAcudiente = false,
            Fecha = "2026-01-15"
        };

        var result = await Notes.CreateNoteAsync(student.Id, tu.Id, dto);

        Assert.True(result.Success);
        Assert.Equal("Título", result.Data!.Titulo);
        Assert.Equal("Logro", result.Data.Tipo);
        Assert.Equal("Leer", result.Data.RecomendacionCasa);
        Assert.False(result.Data.VisibleAcudiente);
        Assert.Equal(new DateTime(2026, 1, 15), result.Data.FechaRegistro.Date);
    }

    [Fact]
    public async Task Create_FechaInvalida_Falla()
    {
        var (tu, _, _, _, student) = await SeedAsync();
        var dto = Dto();
        dto.Fecha = "no-es-fecha";

        var result = await Notes.CreateNoteAsync(student.Id, tu.Id, dto);

        Assert.False(result.Success);
        Assert.Contains("formato", result.Message);
    }

    [Fact]
    public async Task Create_FechaFutura_Falla()
    {
        var (tu, _, _, _, student) = await SeedAsync();
        var dto = Dto();
        dto.Fecha = DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-dd");

        var result = await Notes.CreateNoteAsync(student.Id, tu.Id, dto);

        Assert.False(result.Success);
        Assert.Contains("futura", result.Message);
    }

    [Fact]
    public async Task Create_AsignacionAjenaOInexistente_Falla()
    {
        var (tu, _, _, _, student) = await SeedAsync();
        var dto = Dto();
        dto.AssignmentId = Guid.NewGuid();

        var result = await Notes.CreateNoteAsync(student.Id, tu.Id, dto);

        Assert.False(result.Success);
        Assert.Contains("no existe", result.Message);
    }

    [Fact]
    public async Task Create_ConAsignacionValida_LaVincula()
    {
        var (tu, teacher, _, _, student) = await SeedAsync();
        var activity = await _host.CreateActivityAsync(await _host.CreateModuleAsync(), teacher);
        var assignment = await _host.CreateAssignmentAsync(activity, student);
        var dto = Dto();
        dto.AssignmentId = assignment.Id;

        var result = await Notes.CreateNoteAsync(student.Id, tu.Id, dto);

        Assert.True(result.Success);
        Assert.Equal(assignment.Id, result.Data!.AssignmentId);
    }

    // ------------------------------------------------------------ listar

    [Fact]
    public async Task GetNotes_EstudianteInexistente_Falla()
    {
        Assert.False((await Notes.GetNotesByStudentIdAsync(Guid.NewGuid(), Guid.NewGuid(), true)).Success);
    }

    [Fact]
    public async Task GetNotes_AcudienteNoTitular_Falla()
    {
        var (_, _, _, _, student) = await SeedAsync();

        var result = await Notes.GetNotesByStudentIdAsync(student.Id, Guid.NewGuid(), isStaff: false);

        Assert.False(result.Success);
        Assert.Contains("autorización", result.Message);
    }

    [Fact]
    public async Task GetNotes_EstudianteSinAcudiente_FallaParaNoStaff()
    {
        var (_, s) = await _host.CreateStudentAsync();

        Assert.False((await Notes.GetNotesByStudentIdAsync(s.Id, Guid.NewGuid(), isStaff: false)).Success);
    }

    [Fact]
    public async Task GetNotes_Staff_VeTodasYAcudienteSoloLasVisibles()
    {
        var (tu, _, gu, _, student) = await SeedAsync();
        await Notes.CreateNoteAsync(student.Id, tu.Id, new CreatePedagogicalNoteDto { Contenido = "pública", VisibleAcudiente = true });
        await Notes.CreateNoteAsync(student.Id, tu.Id, new CreatePedagogicalNoteDto { Contenido = "privada", VisibleAcudiente = false });

        var staff = await Notes.GetNotesByStudentIdAsync(student.Id, tu.Id, isStaff: true);
        var guardian = await Notes.GetNotesByStudentIdAsync(student.Id, gu.Id, isStaff: false);

        Assert.Equal(2, staff.Data!.Count);
        Assert.Single(guardian.Data!);
        Assert.Equal("pública", guardian.Data![0].Contenido);
    }

    // ------------------------------------------------------------ editar

    [Fact]
    public async Task Update_NotaInexistente_Falla()
    {
        Assert.False((await Notes.UpdateNoteAsync(Guid.NewGuid(), Guid.NewGuid(), true, Dto())).Success);
    }

    [Fact]
    public async Task Update_OtroDocente_Falla()
    {
        var (tu, _, _, _, student) = await SeedAsync();
        var created = await Notes.CreateNoteAsync(student.Id, tu.Id, Dto());
        var (otro, _) = await _host.CreateTeacherAsync();

        var result = await Notes.UpdateNoteAsync(created.Data!.Id, otro.Id, false, Dto());

        Assert.False(result.Success);
        Assert.Contains("autorización", result.Message);
    }

    [Fact]
    public async Task Update_Autor_ActualizaCampos()
    {
        var (tu, _, _, _, student) = await SeedAsync();
        var created = await Notes.CreateNoteAsync(student.Id, tu.Id, Dto());
        var dto = new CreatePedagogicalNoteDto
        {
            Contenido = " Nuevo ",
            Titulo = " T ",
            Tipo = "Alerta",
            Area = "Sensorial",
            RecomendacionCasa = "Descansar",
            VisibleAcudiente = false,
            Fecha = "2026-02-01"
        };

        var result = await Notes.UpdateNoteAsync(created.Data!.Id, tu.Id, false, dto);

        Assert.True(result.Success);
        Assert.Equal("Nuevo", result.Data!.Contenido);
        Assert.Equal("Alerta", result.Data.Tipo);
        Assert.Equal("Sensorial", result.Data.Area);
        Assert.False(result.Data.VisibleAcudiente);
        Assert.Equal(new DateTime(2026, 2, 1), result.Data.FechaRegistro.Date);
    }

    [Fact]
    public async Task Update_SinCamposOpcionales_ConservaTipoAreaYVisibilidad()
    {
        var (tu, _, _, _, student) = await SeedAsync();
        var created = await Notes.CreateNoteAsync(student.Id, tu.Id,
            new CreatePedagogicalNoteDto { Contenido = "x", Tipo = "Logro", Area = "Social", VisibleAcudiente = false });

        var result = await Notes.UpdateNoteAsync(created.Data!.Id, tu.Id, false, Dto("y"));

        Assert.True(result.Success);
        Assert.Equal("Logro", result.Data!.Tipo);
        Assert.Equal("Social", result.Data.Area);
        Assert.False(result.Data.VisibleAcudiente);
    }

    [Fact]
    public async Task Update_AdminPuedeEditarNotaAjena()
    {
        var (tu, _, _, _, student) = await SeedAsync();
        var created = await Notes.CreateNoteAsync(student.Id, tu.Id, Dto());

        Assert.True((await Notes.UpdateNoteAsync(created.Data!.Id, Guid.NewGuid(), true, Dto("admin"))).Success);
    }

    [Fact]
    public async Task Update_FechaInvalidaOFutura_Falla()
    {
        var (tu, _, _, _, student) = await SeedAsync();
        var created = await Notes.CreateNoteAsync(student.Id, tu.Id, Dto());
        var invalid = Dto();
        invalid.Fecha = "xx";
        var future = Dto();
        future.Fecha = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd");

        Assert.Contains("formato", (await Notes.UpdateNoteAsync(created.Data!.Id, tu.Id, false, invalid)).Message);
        Assert.Contains("futura", (await Notes.UpdateNoteAsync(created.Data!.Id, tu.Id, false, future)).Message);
    }

    [Fact]
    public async Task Update_AsignacionInvalida_Falla()
    {
        var (tu, _, _, _, student) = await SeedAsync();
        var created = await Notes.CreateNoteAsync(student.Id, tu.Id, Dto());
        var dto = Dto();
        dto.AssignmentId = Guid.NewGuid();

        Assert.False((await Notes.UpdateNoteAsync(created.Data!.Id, tu.Id, false, dto)).Success);
    }

    // ------------------------------------------------------------ eliminar

    [Fact]
    public async Task Delete_Inexistente_Falla()
    {
        Assert.False((await Notes.DeleteNoteAsync(Guid.NewGuid(), Guid.NewGuid(), true)).Success);
    }

    [Fact]
    public async Task Delete_OtroDocente_Falla()
    {
        var (tu, _, _, _, student) = await SeedAsync();
        var created = await Notes.CreateNoteAsync(student.Id, tu.Id, Dto());
        var (otro, _) = await _host.CreateTeacherAsync();

        Assert.False((await Notes.DeleteNoteAsync(created.Data!.Id, otro.Id, false)).Success);
    }

    [Fact]
    public async Task Delete_Autor_HaceBajaLogicaYNoSePuedeRepetir()
    {
        var (tu, _, _, _, student) = await SeedAsync();
        var created = await Notes.CreateNoteAsync(student.Id, tu.Id, Dto());

        var first = await Notes.DeleteNoteAsync(created.Data!.Id, tu.Id, false);
        var second = await Notes.DeleteNoteAsync(created.Data!.Id, tu.Id, false);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.False((await _host.Context.PedagogicalNotes.AsNoTracking().SingleAsync()).IsActive);
    }

    // ------------------------------------------------------------ paginado

    private async Task SeedManyNotesAsync(ApplicationUser teacherUser, StudentProfile student)
    {
        for (var i = 0; i < 5; i++)
        {
            await Notes.CreateNoteAsync(student.Id, teacherUser.Id, new CreatePedagogicalNoteDto
            {
                Contenido = $"n{i}",
                Tipo = i % 2 == 0 ? "Logro" : "Alerta",
                Area = i < 3 ? "Social" : "Académica",
                VisibleAcudiente = i != 4,
                Fecha = $"2026-01-{10 + i:00}"
            });
        }
    }

    [Fact]
    public async Task Paged_EstudianteInexistente_Falla()
    {
        Assert.False((await Notes.GetObservationsPagedForGuardianAsync(Guid.NewGuid(), Guid.NewGuid(), true)).Success);
    }

    [Fact]
    public async Task Paged_AcudienteNoTitular_Falla()
    {
        var (_, _, _, _, student) = await SeedAsync();

        Assert.False((await Notes.GetObservationsPagedForGuardianAsync(student.Id, Guid.NewGuid(), false)).Success);
    }

    [Fact]
    public async Task Paged_AcudienteSoloVeVisiblesYOrdenadasPorFechaDescendente()
    {
        var (tu, _, gu, _, student) = await SeedAsync();
        await SeedManyNotesAsync(tu, student);

        var result = await Notes.GetObservationsPagedForGuardianAsync(student.Id, gu.Id, false);

        Assert.True(result.Success);
        Assert.Equal(4, result.Data!.Total);
        Assert.Equal("n3", result.Data.Items[0].Contenido);
        Assert.False(result.Data.HasNextPage);
    }

    [Fact]
    public async Task Paged_AdminVeTodasYPagina()
    {
        var (tu, _, _, _, student) = await SeedAsync();
        await SeedManyNotesAsync(tu, student);

        var result = await Notes.GetObservationsPagedForGuardianAsync(student.Id, Guid.NewGuid(), true, page: 2, pageSize: 2);

        Assert.Equal(5, result.Data!.Total);
        Assert.Equal(2, result.Data.Items.Count);
        Assert.Equal(3, result.Data.TotalPages);
        Assert.True(result.Data.HasNextPage);
        Assert.True(result.Data.HasPreviousPage);
    }

    [Fact]
    public async Task Paged_FiltraPorTipoYArea_YNormalizaPaginacion()
    {
        var (tu, _, _, _, student) = await SeedAsync();
        await SeedManyNotesAsync(tu, student);

        var byType = await Notes.GetObservationsPagedForGuardianAsync(student.Id, Guid.NewGuid(), true, tipo: " logro ");
        var byArea = await Notes.GetObservationsPagedForGuardianAsync(student.Id, Guid.NewGuid(), true, area: "ACADÉMICA");
        var normalized = await Notes.GetObservationsPagedForGuardianAsync(student.Id, Guid.NewGuid(), true, page: -3, pageSize: 1000);

        Assert.Equal(3, byType.Data!.Total);
        Assert.Equal(2, byArea.Data!.Total);
        Assert.Equal(1, normalized.Data!.Page);
        Assert.Equal(100, normalized.Data.PageSize);
    }
}
