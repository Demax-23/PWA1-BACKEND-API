using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Services.DTOs.Activities;
using TeaLearning.Services.DTOs.Execution;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Tests;

public class ActivityServiceTests : IDisposable
{
    private readonly AppTestHost _host = new();
    private IActivityService Svc => _host.Get<IActivityService>();

    public void Dispose() => _host.Dispose();

    private const string ValidSequence = "{\"pasos\":[\"Mojar manos\",\"Enjabonar\",\"Enjuagar\"]}";
    private const string ValidPairs = "{\"parejas\":[{\"leftLabel\":\"a\",\"rightLabel\":\"b\"},{\"leftLabel\":\"c\",\"rightLabel\":\"d\"}]}";

    private static CreateActivityDto Create(Guid moduleId, TipoActividad tipo = TipoActividad.SecuenciaLogica, string? json = null) => new()
    {
        Titulo = "  Lavado de manos ",
        Descripcion = " Desc ",
        ObjetivoPedagogico = " Obj ",
        TipoActividad = tipo,
        ConfigJson = json ?? ValidSequence,
        ModuloId = moduleId
    };

    private static UpdateActivityDto Update(Guid moduleId, TipoActividad tipo = TipoActividad.SecuenciaLogica, string? json = null) => new()
    {
        Titulo = " Nuevo ",
        Descripcion = " D ",
        ObjetivoPedagogico = " O ",
        TipoActividad = tipo,
        ConfigJson = json ?? ValidSequence,
        ModuloId = moduleId
    };

    // ------------------------------------------------------------ crear

    [Fact]
    public async Task Create_ModuloInexistente_Falla()
    {
        var result = await Svc.CreateActivityAsync(Guid.NewGuid(), Create(Guid.NewGuid()));

        Assert.False(result.Success);
        Assert.Contains("no existe", result.Message);
    }

    [Fact]
    public async Task Create_UsuarioInexistenteOInactivo_Falla()
    {
        var module = await _host.CreateModuleAsync();
        var inactive = await _host.CreateUserAsync("inac@test.com", "Docente", isActive: false);

        Assert.False((await Svc.CreateActivityAsync(Guid.NewGuid(), Create(module.Id))).Success);
        Assert.False((await Svc.CreateActivityAsync(inactive.Id, Create(module.Id))).Success);
    }

    [Fact]
    public async Task Create_UsuarioSinPerfilDocente_CreaPerfilYActividad()
    {
        var module = await _host.CreateModuleAsync();
        var admin = await _host.CreateAdminAsync();

        var result = await Svc.CreateActivityAsync(admin.Id, Create(module.Id));

        Assert.True(result.Success);
        Assert.Equal("Lavado de manos", result.Data!.Titulo);
        Assert.Equal("Obj", result.Data.ObjetivoPedagogico);
        Assert.True(await _host.Context.TeacherProfiles.AnyAsync(t => t.UserId == admin.Id));
        Assert.Equal(module.Titulo, result.Data.ModuloTitulo);
    }

    [Fact]
    public async Task Create_DocenteExistente_UsaSuPerfil()
    {
        var module = await _host.CreateModuleAsync();
        var (user, teacher) = await _host.CreateTeacherAsync();

        var result = await Svc.CreateActivityAsync(user.Id, Create(module.Id, TipoActividad.EmparejamientoVisual, ValidPairs));

        Assert.True(result.Success);
        Assert.Equal(teacher.Id, result.Data!.DocenteId);
        Assert.Equal(user.NombreCompleto, result.Data.DocenteNombre);
    }

    [Fact]
    public async Task Create_ComoInactiva_SePersisteInactivaYSeRecargaIgual()
    {
        var module = await _host.CreateModuleAsync();
        var (user, _) = await _host.CreateTeacherAsync();
        var dto = Create(module.Id);
        dto.IsActive = false;

        var result = await Svc.CreateActivityAsync(user.Id, dto);

        Assert.True(result.Success);
        Assert.False(result.Data!.IsActive);
    }

    public static IEnumerable<object[]> InvalidConfigs() => new[]
    {
        new object[] { TipoActividad.RutinaSocial, "", "obligatoria" },
        new object[] { TipoActividad.RutinaSocial, "   ", "obligatoria" },
        new object[] { TipoActividad.RutinaSocial, "{\"a\":\"<script>alert(1)</script>\"}", "no permitido" },
        new object[] { TipoActividad.RutinaSocial, "{\"a\":\"javascript:void(0)\"}", "no permitido" },
        new object[] { TipoActividad.RutinaSocial, "{no es json", "no es un JSON válido" },
        new object[] { TipoActividad.RutinaSocial, "[1,2,3]", "objeto JSON" },
        new object[] { TipoActividad.RutinaSocial, "{}", "vacía" },
        new object[] { TipoActividad.SecuenciaLogica, "{\"otro\":1}", "lista de pasos" },
        new object[] { TipoActividad.SecuenciaLogica, "{\"pasos\":[\"uno\"]}", "al menos 2 pasos" },
        new object[] { TipoActividad.SecuenciaLogica, "{\"pasos\":[\"uno\",\"  \",{\"x\":1}]}", "al menos 2 pasos" },
        new object[] { TipoActividad.EmparejamientoVisual, "{\"otro\":1}", "lista de parejas" },
        new object[] { TipoActividad.EmparejamientoVisual, "{\"parejas\":[{\"leftLabel\":\"a\"},{\"leftLabel\":\"b\",\"rightLabel\":\"c\"}]}", "ambos elementos" },
        new object[] { TipoActividad.EmparejamientoVisual, "{\"parejas\":[1,{\"leftLabel\":\"b\",\"rightLabel\":\"c\"}]}", "al menos 2 parejas" },
    };

    [Theory]
    [MemberData(nameof(InvalidConfigs))]
    public async Task Create_ConfigInvalida_Falla(TipoActividad tipo, string json, string expected)
    {
        var module = await _host.CreateModuleAsync();
        var (user, _) = await _host.CreateTeacherAsync();

        var result = await Svc.CreateActivityAsync(user.Id, Create(module.Id, tipo, json));

        Assert.False(result.Success);
        Assert.Contains(expected, result.Message);
    }

    [Fact]
    public async Task Create_ConfigDemasiadoLarga_Falla()
    {
        var module = await _host.CreateModuleAsync();
        var (user, _) = await _host.CreateTeacherAsync();
        var json = "{\"x\":\"" + new string('a', 17000) + "\"}";

        var result = await Svc.CreateActivityAsync(user.Id, Create(module.Id, TipoActividad.RutinaSocial, json));

        Assert.False(result.Success);
    }

    [Theory]
    [InlineData("{\"steps\":[{\"label\":\"uno\"},{\"label\":\"dos\"}]}")]
    [InlineData("{\"pasos\":[\"uno\",\"dos\"]}")]
    public async Task Create_SecuenciaConFormatosAlternativosDePasos_EsValida(string json)
    {
        var module = await _host.CreateModuleAsync();
        var (user, _) = await _host.CreateTeacherAsync();

        Assert.True((await Svc.CreateActivityAsync(user.Id, Create(module.Id, TipoActividad.SecuenciaLogica, json))).Success);
    }

    [Theory]
    [InlineData("{\"pairs\":[{\"emocion\":\"feliz\",\"pareja\":\"sonrisa\"},{\"elementoA\":\"x\",\"elementoB\":\"y\"}]}")]
    [InlineData("{\"pares\":[{\"leftLabel\":\"a\",\"color\":\"rojo\"},{\"leftLabel\":\"b\",\"rightLabel\":\"c\"},{}]}")]
    public async Task Create_EmparejamientoConAliasDeCampos_EsValido(string json)
    {
        var module = await _host.CreateModuleAsync();
        var (user, _) = await _host.CreateTeacherAsync();

        Assert.True((await Svc.CreateActivityAsync(user.Id, Create(module.Id, TipoActividad.EmparejamientoVisual, json))).Success);
    }

    // ------------------------------------------------------------ consultar

    [Fact]
    public async Task Get_FiltraPorModuloTipoDocenteYEstado()
    {
        var (_, teacher) = await _host.CreateTeacherAsync();
        var (_, other) = await _host.CreateTeacherAsync();
        var m1 = await _host.CreateModuleAsync(name: "M1");
        var m2 = await _host.CreateModuleAsync(name: "M2");
        await _host.CreateActivityAsync(m1, teacher, "A", TipoActividad.SecuenciaLogica);
        await _host.CreateActivityAsync(m2, other, "B", TipoActividad.RutinaSocial);
        await _host.CreateActivityAsync(m1, teacher, "C", TipoActividad.SecuenciaLogica, active: false);

        Assert.Equal(2, (await Svc.GetActivitiesAsync()).Data!.Count);
        Assert.Single((await Svc.GetActivitiesAsync(moduloId: m1.Id)).Data!);
        Assert.Single((await Svc.GetActivitiesAsync(tipoActividad: TipoActividad.RutinaSocial)).Data!);
        Assert.Single((await Svc.GetActivitiesAsync(docenteId: other.Id)).Data!);
        Assert.Equal(3, (await Svc.GetActivitiesAsync(onlyActive: false)).Data!.Count);
    }

    [Fact]
    public async Task GetById_ExistenteEInexistenteEInactiva()
    {
        var (_, teacher) = await _host.CreateTeacherAsync();
        var module = await _host.CreateModuleAsync();
        var ok = await _host.CreateActivityAsync(module, teacher);
        var inactive = await _host.CreateActivityAsync(module, teacher, "I", active: false);

        Assert.True((await Svc.GetActivityByIdAsync(ok.Id)).Success);
        Assert.False((await Svc.GetActivityByIdAsync(Guid.NewGuid())).Success);
        Assert.False((await Svc.GetActivityByIdAsync(inactive.Id)).Success);
    }

    // ------------------------------------------------------------ actualizar

    [Fact]
    public async Task Update_Inexistente_Falla()
    {
        Assert.False((await Svc.UpdateActivityAsync(Guid.NewGuid(), Guid.NewGuid(), Update(Guid.NewGuid()))).Success);
    }

    [Fact]
    public async Task Update_OtroDocente_Falla_YUsuarioSinPerfilTambien()
    {
        var (_, owner) = await _host.CreateTeacherAsync();
        var (otherUser, _) = await _host.CreateTeacherAsync();
        var module = await _host.CreateModuleAsync();
        var activity = await _host.CreateActivityAsync(module, owner);

        var other = await Svc.UpdateActivityAsync(activity.Id, otherUser.Id, Update(module.Id));
        var noProfile = await Svc.UpdateActivityAsync(activity.Id, Guid.NewGuid(), Update(module.Id));

        Assert.False(other.Success);
        Assert.Contains("otro docente", other.Message);
        Assert.False(noProfile.Success);
    }

    [Fact]
    public async Task Update_ModuloInexistente_Falla()
    {
        var (user, owner) = await _host.CreateTeacherAsync();
        var activity = await _host.CreateActivityAsync(await _host.CreateModuleAsync(), owner);

        var result = await Svc.UpdateActivityAsync(activity.Id, user.Id, Update(Guid.NewGuid()));

        Assert.False(result.Success);
        Assert.Contains("no existe", result.Message);
    }

    [Fact]
    public async Task Update_ConfigInvalida_Falla()
    {
        var (user, owner) = await _host.CreateTeacherAsync();
        var module = await _host.CreateModuleAsync();
        var activity = await _host.CreateActivityAsync(module, owner);

        var result = await Svc.UpdateActivityAsync(activity.Id, user.Id, Update(module.Id, TipoActividad.SecuenciaLogica, "{}"));

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Update_Autor_ActualizaCamposYEstado()
    {
        var (user, owner) = await _host.CreateTeacherAsync();
        var module = await _host.CreateModuleAsync();
        var target = await _host.CreateModuleAsync(name: "Destino");
        var activity = await _host.CreateActivityAsync(module, owner);
        var dto = Update(target.Id, TipoActividad.EmparejamientoVisual, ValidPairs);
        dto.IsActive = false;

        var result = await Svc.UpdateActivityAsync(activity.Id, user.Id, dto);

        Assert.True(result.Success);
        Assert.Equal("Nuevo", result.Data!.Titulo);
        Assert.Equal(TipoActividad.EmparejamientoVisual, result.Data.TipoActividad);
        Assert.Equal(target.Id, result.Data.ModuloId);
        Assert.False(result.Data.IsActive);
    }

    [Fact]
    public async Task Update_SinIsActive_ConservaEstado_YAdminPuedeEditarAjena()
    {
        var (_, owner) = await _host.CreateTeacherAsync();
        var module = await _host.CreateModuleAsync();
        var activity = await _host.CreateActivityAsync(module, owner);

        var result = await Svc.UpdateActivityAsync(activity.Id, Guid.NewGuid(), Update(module.Id), isAdmin: true);

        Assert.True(result.Success);
        Assert.True(result.Data!.IsActive);
    }

    // ------------------------------------------------------------ eliminar

    [Fact]
    public async Task Delete_Inexistente_Falla()
    {
        Assert.False((await Svc.DeleteActivityAsync(Guid.NewGuid(), Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task Delete_OtroDocente_Falla()
    {
        var (_, owner) = await _host.CreateTeacherAsync();
        var (otherUser, _) = await _host.CreateTeacherAsync();
        var activity = await _host.CreateActivityAsync(await _host.CreateModuleAsync(), owner);

        var result = await Svc.DeleteActivityAsync(activity.Id, otherUser.Id);

        Assert.False(result.Success);
        Assert.Contains("otro docente", result.Message);
    }

    [Fact]
    public async Task Delete_AutorYAdmin_HacenBajaLogica()
    {
        var (user, owner) = await _host.CreateTeacherAsync();
        var module = await _host.CreateModuleAsync();
        var a1 = await _host.CreateActivityAsync(module, owner, "A1");
        var a2 = await _host.CreateActivityAsync(module, owner, "A2");

        Assert.True((await Svc.DeleteActivityAsync(a1.Id, user.Id)).Success);
        Assert.True((await Svc.DeleteActivityAsync(a2.Id, Guid.NewGuid(), isAdmin: true)).Success);
        Assert.All(_host.Context.Activities.AsNoTracking().ToList(), a => Assert.False(a.IsActive));
    }
}

public class ActivityExecutionServiceTests : IDisposable
{
    private readonly AppTestHost _host = new();
    private IActivityExecutionService Svc => _host.Get<IActivityExecutionService>();

    public void Dispose() => _host.Dispose();

    private async Task<(ApplicationUser StudentUser, StudentProfile Student, ActivityAssignment Assignment)> SeedAsync(
        bool withSensory = true, Action<SensoryPreference>? tweak = null)
    {
        var (_, teacher) = await _host.CreateTeacherAsync();
        var (su, s) = await _host.CreateStudentAsync(withSensory: withSensory);
        if (tweak != null)
        {
            var pref = await _host.Context.SensoryPreferences.SingleAsync();
            tweak(pref);
            await _host.Context.SaveChangesAsync();
        }
        var activity = await _host.CreateActivityAsync(await _host.CreateModuleAsync(), teacher);
        var assignment = await _host.CreateAssignmentAsync(activity, s);
        return (su, s, assignment);
    }

    [Fact]
    public async Task Start_Inexistente_Falla()
    {
        Assert.False((await Svc.StartActivityAsync(Guid.NewGuid(), Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task Start_DeOtroEstudiante_Falla()
    {
        var (_, _, assignment) = await SeedAsync();

        var result = await Svc.StartActivityAsync(assignment.Id, Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Contains("autorización", result.Message);
    }

    [Fact]
    public async Task Start_PendienteTransicionaAEnProgresoYDevuelveAdaptaciones()
    {
        var (user, _, assignment) = await SeedAsync();

        var result = await Svc.StartActivityAsync(assignment.Id, user.Id);

        Assert.True(result.Success);
        Assert.Equal("EnProgreso", result.Data!.Estado);
        Assert.NotNull(result.Data.SensoryAdaptations);
        Assert.Equal("estandar", result.Data.SensoryAdaptations!.ModoTemporizador);
        Assert.Equal(EstadoAsignacion.EnProgreso, (await _host.Context.ActivityAssignments.AsNoTracking().SingleAsync()).Estado);
    }

    [Fact]
    public async Task Start_SinPreferenciasSensoriales_NoDevuelveAdaptaciones()
    {
        var (user, _, assignment) = await SeedAsync(withSensory: false);

        var result = await Svc.StartActivityAsync(assignment.Id, user.Id);

        Assert.True(result.Success);
        Assert.Null(result.Data!.SensoryAdaptations);
    }

    [Theory]
    [InlineData(true, "estandar", "sin-temporizador")]
    [InlineData(false, "sin-temporizador", "tiempo-extendido")]
    [InlineData(false, "", "tiempo-extendido")]
    [InlineData(false, "tiempo-extendido", "tiempo-extendido")]
    public async Task Start_ResuelveModoTemporizador(bool ocultar, string modo, string esperado)
    {
        var (user, _, assignment) = await SeedAsync(tweak: p => { p.OcultarTemporizadores = ocultar; p.ModoTemporizador = modo; p.SegundosAvisoPrevio = 999; });

        var result = await Svc.StartActivityAsync(assignment.Id, user.Id);

        Assert.Equal(esperado, result.Data!.SensoryAdaptations!.ModoTemporizador);
        Assert.Equal(60, result.Data.SensoryAdaptations.SegundosAvisoPrevio);
    }

    [Fact]
    public async Task Start_YaEnProgreso_NoCambiaEstado()
    {
        var (user, _, assignment) = await SeedAsync();
        assignment.Estado = EstadoAsignacion.Completada;
        await _host.Context.SaveChangesAsync();

        var result = await Svc.StartActivityAsync(assignment.Id, user.Id);

        Assert.Equal("Completada", result.Data!.Estado);
    }

    [Fact]
    public async Task Submit_Inexistente_Falla()
    {
        Assert.False((await Svc.SubmitActivityAsync(Guid.NewGuid(), Guid.NewGuid(), new SubmitActivityDto())).Success);
    }

    [Fact]
    public async Task Submit_DeOtroEstudiante_Falla()
    {
        var (_, _, assignment) = await SeedAsync();

        var result = await Svc.SubmitActivityAsync(assignment.Id, Guid.NewGuid(), new SubmitActivityDto { CantidadAciertos = 1 });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Submit_CompletadaConCeroAciertos_Falla()
    {
        var (user, _, assignment) = await SeedAsync();

        var result = await Svc.SubmitActivityAsync(assignment.Id, user.Id, new SubmitActivityDto { CompletadaExitosamente = true, CantidadAciertos = 0 });

        Assert.False(result.Success);
        Assert.Contains("al menos 1 acierto", result.Message);
    }

    [Fact]
    public async Task Submit_Exitoso_RegistraEjecucionYCompletaAsignacion()
    {
        var (user, student, assignment) = await SeedAsync();

        var result = await Svc.SubmitActivityAsync(assignment.Id, user.Id,
            new SubmitActivityDto { CompletadaExitosamente = true, CantidadAciertos = 4, CantidadIntentosFallidos = 1, TiempoRespuestaSegundos = 42 });

        Assert.True(result.Success);
        Assert.Equal("Completada", result.Data!.NuevoEstadoAsignacion);
        Assert.Contains("Excelente", result.Message);
        var execution = await _host.Context.ActivityExecutions.SingleAsync();
        Assert.Equal(student.Id, execution.StudentProfileId);
        Assert.Equal(42, execution.TiempoRespuestaSegundos);
    }

    [Fact]
    public async Task Submit_IntentoFallido_RegistraSinCompletar()
    {
        var (user, _, assignment) = await SeedAsync();

        var result = await Svc.SubmitActivityAsync(assignment.Id, user.Id,
            new SubmitActivityDto { CompletadaExitosamente = false, CantidadAciertos = 0, CantidadIntentosFallidos = 3 });

        Assert.True(result.Success);
        Assert.Equal("Pendiente", result.Data!.NuevoEstadoAsignacion);
        Assert.Contains("Intento registrado", result.Message);
    }
}

public class GuardianMonitoringServiceTests : IDisposable
{
    private readonly AppTestHost _host = new();
    private IGuardianMonitoringService Svc => _host.Get<IGuardianMonitoringService>();

    public void Dispose() => _host.Dispose();

    private async Task<(ApplicationUser GuardianUser, StudentProfile Student, Activity Activity)> SeedAsync()
    {
        var (_, teacher) = await _host.CreateTeacherAsync();
        var (gu, g) = await _host.CreateGuardianAsync();
        var (_, s) = await _host.CreateStudentAsync(guardian: g);
        var activity = await _host.CreateActivityAsync(await _host.CreateModuleAsync(), teacher);
        return (gu, s, activity);
    }

    private async Task AddExecutionAsync(StudentProfile s, ActivityAssignment a, DateTime date, bool ok, int aciertos, int fallos, int segundos)
    {
        _host.Context.ActivityExecutions.Add(new ActivityExecution
        {
            AssignmentId = a.Id,
            StudentProfileId = s.Id,
            FechaRegistro = date,
            CompletadaExitosamente = ok,
            CantidadAciertos = aciertos,
            CantidadIntentosFallidos = fallos,
            TiempoRespuestaSegundos = segundos
        });
        await _host.Context.SaveChangesAsync();
    }

    [Fact]
    public async Task Progress_EstudianteInexistente_Falla()
    {
        Assert.False((await Svc.GetStudentProgressAsync(Guid.NewGuid(), Guid.NewGuid(), true)).Success);
    }

    [Fact]
    public async Task Progress_AcudienteNoTitular_Falla_YSinAcudienteTambien()
    {
        var (_, student, _) = await SeedAsync();
        var (_, orphan) = await _host.CreateStudentAsync();

        Assert.False((await Svc.GetStudentProgressAsync(student.Id, Guid.NewGuid(), false)).Success);
        Assert.False((await Svc.GetStudentProgressAsync(orphan.Id, Guid.NewGuid(), false)).Success);
    }

    [Fact]
    public async Task Progress_SinActividades_MensajeDeInicio()
    {
        var (gu, student, _) = await SeedAsync();

        var result = await Svc.GetStudentProgressAsync(student.Id, gu.Id, false);

        Assert.True(result.Success);
        Assert.Equal(0, result.Data!.PorcentajeAvance);
        Assert.Contains("cuenta con actividades preparadas", result.Data.MensajePedagogico);
        Assert.Empty(result.Data.Historico);
    }

    [Theory]
    [InlineData(1, 4, "Buen comienzo")]
    [InlineData(2, 4, "Muy buen ritmo")]
    [InlineData(4, 4, "Excelente avance")]
    public async Task Progress_MensajeSegunPorcentaje(int completadas, int total, string esperado)
    {
        var (gu, student, _) = await SeedAsync();
        var (_, teacher) = await _host.CreateTeacherAsync();
        var module = await _host.CreateModuleAsync(name: "X");
        for (var i = 0; i < total; i++)
        {
            var act = await _host.CreateActivityAsync(module, teacher, "A" + i);
            await _host.CreateAssignmentAsync(act, student, i < completadas ? EstadoAsignacion.Completada : EstadoAsignacion.Pendiente, orden: i + 1);
        }

        var result = await Svc.GetStudentProgressAsync(student.Id, gu.Id, false);

        Assert.Contains(esperado, result.Data!.MensajePedagogico);
        Assert.Equal(total, result.Data.ActividadesAsignadasTotal);
        Assert.Equal(completadas, result.Data.ActividadesCompletadas);
    }

    [Fact]
    public async Task Progress_AdminCalculaTiempoUltimasActividadesYHistorico()
    {
        var (_, student, activity) = await SeedAsync();
        var assignment = await _host.CreateAssignmentAsync(activity, student, EstadoAsignacion.EnProgreso);
        // Semana A (lunes 2026-01-05) y semana B (lunes 2026-01-12), incluido un domingo para cubrir el cálculo de lunes.
        await AddExecutionAsync(student, assignment, new DateTime(2026, 1, 5, 10, 0, 0), true, 3, 1, 120);
        await AddExecutionAsync(student, assignment, new DateTime(2026, 1, 11, 10, 0, 0), false, 0, 0, 60);   // domingo → semana A
        await AddExecutionAsync(student, assignment, new DateTime(2026, 1, 12, 10, 0, 0), true, 5, 0, 30);

        var result = await Svc.GetStudentProgressAsync(student.Id, Guid.NewGuid(), true);

        Assert.True(result.Success);
        Assert.Equal(3.5, result.Data!.TiempoTotalInvertidoMinutos);
        Assert.Equal(1, result.Data.ActividadesEnProgreso);
        Assert.Equal(2, result.Data.Historico.Count);
        Assert.Equal("2026-01-05", result.Data.Historico[0].Periodo);
        Assert.Equal(75.0, result.Data.Historico[0].TasaAciertos); // 3 / (3+1)
        Assert.Equal(3, result.Data.UltimasActividades.Count);
        Assert.Equal(new DateTime(2026, 1, 12, 10, 0, 0), result.Data.UltimasActividades[0].FechaRegistro);
    }

    [Fact]
    public async Task Progress_SemanaSinAciertosNiFallos_TasaDel100()
    {
        var (_, student, activity) = await SeedAsync();
        var assignment = await _host.CreateAssignmentAsync(activity, student);
        await AddExecutionAsync(student, assignment, new DateTime(2026, 2, 2), false, 0, 0, 10);

        var result = await Svc.GetStudentProgressAsync(student.Id, Guid.NewGuid(), true);

        Assert.Equal(100.0, result.Data!.Historico[0].TasaAciertos);
    }

    [Fact]
    public async Task History_Falla_ParaInexistenteYNoTitular_YPagina()
    {
        var (gu, student, activity) = await SeedAsync();
        var assignment = await _host.CreateAssignmentAsync(activity, student);
        for (var i = 0; i < 5; i++)
        {
            await AddExecutionAsync(student, assignment, DateTime.UtcNow.AddMinutes(-i), i % 2 == 0, 1, 0, 5);
        }

        Assert.False((await Svc.GetActivityHistoryAsync(Guid.NewGuid(), gu.Id, false)).Success);
        Assert.False((await Svc.GetActivityHistoryAsync(student.Id, Guid.NewGuid(), false)).Success);

        var page2 = await Svc.GetActivityHistoryAsync(student.Id, gu.Id, false, page: 2, pageSize: 2);
        Assert.Equal(5, page2.Data!.Total);
        Assert.Equal(2, page2.Data.Items.Count);
        Assert.Equal(2, page2.Data.Page);

        var normalized = await Svc.GetActivityHistoryAsync(student.Id, Guid.NewGuid(), true, page: 0, pageSize: 0);
        Assert.Equal(1, normalized.Data!.Page);
        Assert.Equal(1, normalized.Data.PageSize);
    }
}
