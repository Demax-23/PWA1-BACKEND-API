using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Tests;

public class AchievementServiceTests : IDisposable
{
    private readonly AppTestHost _host = new();
    private IAchievementService Svc => _host.Get<IAchievementService>();

    public void Dispose() => _host.Dispose();

    private async Task AddSuccessAsync(StudentProfile student, Activity activity, int minutesAgo, bool success = true)
    {
        var assignment = _host.Context.ActivityAssignments.FirstOrDefault(a => a.ActivityId == activity.Id && a.StudentProfileId == student.Id)
                         ?? await _host.CreateAssignmentAsync(activity, student, success ? EstadoAsignacion.Completada : EstadoAsignacion.EnProgreso);
        _host.Context.ActivityExecutions.Add(new ActivityExecution
        {
            AssignmentId = assignment.Id,
            StudentProfileId = student.Id,
            CompletadaExitosamente = success,
            CantidadAciertos = 3,
            FechaRegistro = DateTime.UtcNow.AddMinutes(-minutesAgo)
        });
        await _host.Context.SaveChangesAsync();
    }

    [Fact]
    public async Task ByUserId_SinPerfil_Falla()
    {
        var result = await Svc.GetStudentAchievementsByUserIdAsync(Guid.NewGuid());

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ById_Inexistente_Falla()
    {
        var result = await Svc.GetStudentAchievementsByIdAsync(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Contains("no encontrado", result.Message);
    }

    [Fact]
    public async Task SinActividades_TodasLasInsigniasBloqueadasYMensajeInicial()
    {
        var (user, student) = await _host.CreateStudentAsync();

        var result = await Svc.GetStudentAchievementsByUserIdAsync(user.Id);

        Assert.True(result.Success);
        Assert.Equal(7, result.Data!.Insignias.Count);
        Assert.All(result.Data.Insignias, b => Assert.False(b.Desbloqueado));
        Assert.Contains("grandes aventuras", result.Data.MensajeMotivacional);
        Assert.Empty(result.Data.Logros);
        Assert.Equal(student.Id, result.Data.EstudianteId);
    }

    [Fact]
    public async Task ConExitosPorTipo_DesbloqueaInsigniasEspecificasYLogros()
    {
        var (_, teacher) = await _host.CreateTeacherAsync();
        var (_, student) = await _host.CreateStudentAsync();
        var module = await _host.CreateModuleAsync(await _host.CreateCategoryAsync());
        var tipos = new[] { TipoActividad.EmparejamientoVisual, TipoActividad.SecuenciaLogica, TipoActividad.AsociacionEmocional, TipoActividad.RutinaSocial };
        var minutes = 100;
        foreach (var tipo in tipos)
        {
            var activity = await _host.CreateActivityAsync(module, teacher, tipo.ToString(), tipo);
            await AddSuccessAsync(student, activity, minutes--);
        }

        var result = await Svc.GetStudentAchievementsByIdAsync(student.Id);

        var badges = result.Data!.Insignias.ToDictionary(b => b.Clave);
        Assert.True(badges["primer_paso"].Desbloqueado);
        Assert.True(badges["explorador_visual"].Desbloqueado);
        Assert.True(badges["maestro_secuencias"].Desbloqueado);
        Assert.True(badges["campeon_calma"].Desbloqueado);
        Assert.True(badges["constructor_vinculos"].Desbloqueado);
        Assert.False(badges["habito_constante"].Desbloqueado);
        Assert.Contains("Buen trabajo", result.Data.MensajeMotivacional);
        Assert.Contains(result.Data.Logros, l => l.Tipo == "PrimeraActividad");
        Assert.Equal(4, result.Data.Logros.Count(l => l.Tipo == "ActividadCompletada"));
        Assert.Contains(result.Data.Logros, l => l.Tipo == "ModuloCompletado");
        Assert.Equal(1, result.Data.TotalModulosConcluidos);
        Assert.Equal("Autonomía", result.Data.Modulos[0].Categoria);
    }

    [Theory]
    [InlineData(5, "Increíble dedicación")]
    [InlineData(10, "Orgullo total")]
    public async Task MensajeYInsigniasDeConstancia_SegunCantidadDeExitos(int exitos, string expectedMessage)
    {
        var (_, teacher) = await _host.CreateTeacherAsync();
        var (_, student) = await _host.CreateStudentAsync();
        var module = await _host.CreateModuleAsync();
        for (var i = 0; i < exitos; i++)
        {
            var activity = await _host.CreateActivityAsync(module, teacher, "A" + i);
            await AddSuccessAsync(student, activity, 200 - i);
        }

        var result = await Svc.GetStudentAchievementsByIdAsync(student.Id);

        Assert.Contains(expectedMessage, result.Data!.MensajeMotivacional);
        Assert.True(result.Data.Insignias.Single(b => b.Clave == "habito_constante").Desbloqueado);
        Assert.Equal(exitos >= 10, result.Data.Insignias.Single(b => b.Clave == "super_perseverante").Desbloqueado);
        Assert.Equal(exitos, result.Data.TotalActividadesCompletadas);
    }

    [Fact]
    public async Task ModuloParcialYSinActividades_NoSeConsideranConcluidos_General()
    {
        var (_, teacher) = await _host.CreateTeacherAsync();
        var (_, student) = await _host.CreateStudentAsync();
        var partial = await _host.CreateModuleAsync(name: "Parcial");
        await _host.CreateModuleAsync(name: "Vacío");
        await _host.CreateModuleAsync(name: "Inactivo", active: false);
        var done = await _host.CreateActivityAsync(partial, teacher, "Hecha");
        await _host.CreateActivityAsync(partial, teacher, "Pendiente");
        await AddSuccessAsync(student, done, 5);
        await AddSuccessAsync(student, done, 3, success: false);

        var result = await Svc.GetStudentAchievementsByIdAsync(student.Id);

        Assert.Equal(0, result.Data!.TotalModulosConcluidos);
        Assert.Equal(2, result.Data.Modulos.Count); // el inactivo no aparece
        Assert.All(result.Data.Modulos, m => Assert.Equal("General", m.Categoria));
        Assert.Equal(1, result.Data.Modulos.Single(m => m.Titulo == "Parcial").ActividadesCompletadas);
    }

    [Fact]
    public async Task AsignacionCompletadaSinEjecucion_CuentaParaElModulo()
    {
        var (_, teacher) = await _host.CreateTeacherAsync();
        var (_, student) = await _host.CreateStudentAsync();
        var module = await _host.CreateModuleAsync();
        var activity = await _host.CreateActivityAsync(module, teacher);
        await _host.CreateAssignmentAsync(activity, student, EstadoAsignacion.Completada);

        var result = await Svc.GetStudentAchievementsByIdAsync(student.Id);

        var moduleDto = Assert.Single(result.Data!.ModulosConcluidos);
        Assert.Equal(1, moduleDto.TotalActividades);
        Assert.Equal(1, result.Data.TotalModulosConcluidos);
    }
}
