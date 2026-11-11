using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TeaLearning.Api.Converters;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Services.DTOs.Sensory;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Tests;

public class SensoryPreferenceServiceTests : IDisposable
{
    private readonly AppTestHost _host = new();
    private ISensoryPreferenceService Svc => _host.Get<ISensoryPreferenceService>();

    public void Dispose() => _host.Dispose();

    private static UpdateSensoryPreferenceDto Dto() => new()
    {
        PaletaColor = "Neutro",
        Tipografia = "Arial",
        NivelEscalaFuente = 3,
        ModoContraste = ModoContraste.AltoSuave
    };

    [Fact]
    public async Task Get_EstudianteInexistente_Falla()
    {
        Assert.False((await Svc.GetSensoryPreferencesByStudentIdAsync(Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task Get_SinPreferenciasPrevias_CreaLasPorDefecto()
    {
        var (_, student) = await _host.CreateStudentAsync(withSensory: false);

        var result = await Svc.GetSensoryPreferencesByStudentIdAsync(student.Id);

        Assert.True(result.Success);
        Assert.Equal("PastelSuave", result.Data!.PaletaColor);
        Assert.Equal("sin-temporizador", result.Data.ModoTemporizador);
        Assert.Equal(1, await _host.Context.SensoryPreferences.CountAsync());
    }

    [Fact]
    public async Task Get_Autorizacion_EstudiantePropioYAcudienteVinculadoSi_OtrosNo()
    {
        var (gu, guardian) = await _host.CreateGuardianAsync();
        var (su, student) = await _host.CreateStudentAsync(guardian: guardian);

        Assert.True((await Svc.GetSensoryPreferencesByStudentIdAsync(student.Id, su.Id, isStaff: false)).Success);
        Assert.True((await Svc.GetSensoryPreferencesByStudentIdAsync(student.Id, gu.Id, isStaff: false)).Success);
        var denied = await Svc.GetSensoryPreferencesByStudentIdAsync(student.Id, Guid.NewGuid(), isStaff: false);
        Assert.False(denied.Success);
        Assert.Contains("autorización", denied.Message);
    }

    [Fact]
    public async Task Get_ResuelveModoTemporizadorMostrado()
    {
        var (_, student) = await _host.CreateStudentAsync();
        var pref = await _host.Context.SensoryPreferences.SingleAsync();
        pref.OcultarTemporizadores = false;
        pref.ModoTemporizador = "sin-temporizador";
        pref.SegundosAvisoPrevio = 500;
        await _host.Context.SaveChangesAsync();

        var result = await Svc.GetSensoryPreferencesByStudentIdAsync(student.Id);

        Assert.Equal("tiempo-extendido", result.Data!.ModoTemporizador);
        Assert.Equal(60, result.Data.SegundosAvisoPrevio);
    }

    [Fact]
    public async Task Update_EstudianteInexistente_Falla()
    {
        Assert.False((await Svc.UpdateSensoryPreferencesAsync(Guid.NewGuid(), Dto())).Success);
    }

    [Fact]
    public async Task Update_AcudienteNoTitular_Falla_YSinAcudiente_Tambien()
    {
        var (_, guardian) = await _host.CreateGuardianAsync();
        var (_, student) = await _host.CreateStudentAsync(guardian: guardian);
        var (_, orphan) = await _host.CreateStudentAsync();

        Assert.False((await Svc.UpdateSensoryPreferencesAsync(student.Id, Dto(), Guid.NewGuid(), isAdmin: false)).Success);
        Assert.False((await Svc.UpdateSensoryPreferencesAsync(orphan.Id, Dto(), Guid.NewGuid(), isAdmin: false)).Success);
    }

    [Fact]
    public async Task Update_AcudienteTitular_ActualizaTodosLosCampos()
    {
        var (gu, guardian) = await _host.CreateGuardianAsync();
        var (_, student) = await _host.CreateStudentAsync(guardian: guardian);
        var dto = Dto();
        dto.VolumenAudio = 80;
        dto.TextoAVoz = false;
        dto.SegundosAvisoPrevio = 200;
        dto.ModoTemporizador = "  ESTANDAR ";

        var result = await Svc.UpdateSensoryPreferencesAsync(student.Id, dto, gu.Id, isAdmin: false);

        Assert.True(result.Success);
        Assert.Equal("Neutro", result.Data!.PaletaColor);
        Assert.Equal(80, result.Data.VolumenAudio);
        Assert.False(result.Data.TextoAVoz);
        Assert.Equal("estandar", result.Data.ModoTemporizador);
        Assert.False(result.Data.OcultarTemporizadores);
        Assert.Equal(60, result.Data.SegundosAvisoPrevio);
    }

    [Fact]
    public async Task Update_ModoSinTemporizador_OcultaTemporizadores()
    {
        var (_, student) = await _host.CreateStudentAsync();
        var dto = Dto();
        dto.ModoTemporizador = "sin-temporizador";

        var result = await Svc.UpdateSensoryPreferencesAsync(student.Id, dto);

        Assert.True(result.Data!.OcultarTemporizadores);
    }

    [Theory]
    [InlineData(true, "estandar", "sin-temporizador")]
    [InlineData(false, "estandar", "estandar")]
    [InlineData(false, "tiempo-extendido", "tiempo-extendido")]
    public async Task Update_SinModoExplicito_DerivaDeOcultarTemporizadores(bool ocultar, string modoActual, string esperado)
    {
        var (_, student) = await _host.CreateStudentAsync();
        var pref = await _host.Context.SensoryPreferences.SingleAsync();
        pref.ModoTemporizador = modoActual;
        await _host.Context.SaveChangesAsync();
        var dto = Dto();
        dto.OcultarTemporizadores = ocultar;
        dto.ModoTemporizador = null;

        var result = await Svc.UpdateSensoryPreferencesAsync(student.Id, dto);

        Assert.Equal(esperado, result.Data!.ModoTemporizador);
    }

    [Fact]
    public async Task Update_SinPreferenciasPrevias_LasCrea()
    {
        var (_, student) = await _host.CreateStudentAsync(withSensory: false);

        var result = await Svc.UpdateSensoryPreferencesAsync(student.Id, Dto());

        Assert.True(result.Success);
        Assert.Equal(1, await _host.Context.SensoryPreferences.CountAsync());
        Assert.Equal(60, result.Data!.VolumenAudio); // conserva el valor por defecto si el DTO no lo envía
    }
}

public class JsonConverterTests
{
    private static JsonSerializerOptions Options(params System.Text.Json.Serialization.JsonConverter[] converters)
    {
        var o = new JsonSerializerOptions();
        foreach (var c in converters) o.Converters.Add(c);
        return o;
    }

    private static readonly JsonSerializerOptions Decimals = Options(new FlexibleDecimalJsonConverter(), new FlexibleNullableDecimalJsonConverter());
    private static readonly JsonSerializerOptions Dates = Options(new UtcDateTimeJsonConverter(), new NullableUtcDateTimeJsonConverter());

    [Theory]
    [InlineData("4.5", 4.5)]
    [InlineData("\"4.5\"", 4.5)]
    [InlineData("\" 4,5 \"", 4.5)]
    [InlineData("3", 3)]
    public void Decimal_AceptaNumeroYCadenasConPuntoOComa(string json, double expected)
    {
        Assert.Equal((decimal)expected, JsonSerializer.Deserialize<decimal>(json, Decimals));
    }

    [Theory]
    [InlineData("\"abc\"")]
    [InlineData("\"\"")]
    [InlineData("true")]
    public void Decimal_ValorInvalido_LanzaJsonException(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<decimal>(json, Decimals));
    }

    [Theory]
    [InlineData("null", null)]
    [InlineData("\"\"", null)]
    [InlineData("\"  \"", null)]
    [InlineData("2.5", 2.5)]
    [InlineData("\"2,5\"", 2.5)]
    public void DecimalNulable_AceptaNuloVacioYValores(string json, double? expected)
    {
        var result = JsonSerializer.Deserialize<decimal?>(json, Decimals);

        Assert.Equal(expected.HasValue ? (decimal?)expected.Value : null, result);
    }

    [Fact]
    public void DecimalNulable_ValorInvalido_Lanza()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<decimal?>("\"xx\"", Decimals));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<decimal?>("false", Decimals));
    }

    [Fact]
    public void Decimal_Escribe_NumeroYNulo()
    {
        Assert.Equal("4.5", JsonSerializer.Serialize(4.5m, Decimals));
        Assert.Equal("4.5", JsonSerializer.Serialize((decimal?)4.5m, Decimals));
        Assert.Equal("null", JsonSerializer.Serialize((decimal?)null, Decimals));
    }

    [Fact]
    public void DateTime_EscribeSiempreConZ_YLeeCadenasYNulos()
    {
        var local = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Unspecified);
        var utc = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);

        Assert.Equal("\"2026-03-04T05:06:07.000Z\"", JsonSerializer.Serialize(local, Dates));
        Assert.Equal("\"2026-03-04T05:06:07.000Z\"", JsonSerializer.Serialize(utc, Dates));
        Assert.Equal("\"2026-03-04T05:06:07.000Z\"", JsonSerializer.Serialize((DateTime?)local, Dates));
        Assert.Equal("\"2026-03-04T05:06:07.000Z\"", JsonSerializer.Serialize((DateTime?)utc, Dates));
        Assert.Equal("null", JsonSerializer.Serialize((DateTime?)null, Dates));

        Assert.Equal(utc, JsonSerializer.Deserialize<DateTime>("\"2026-03-04T05:06:07Z\"", Dates));
        Assert.Equal(utc, JsonSerializer.Deserialize<DateTime?>("\"2026-03-04T05:06:07Z\"", Dates));
        Assert.Null(JsonSerializer.Deserialize<DateTime?>("null", Dates));
        Assert.Null(JsonSerializer.Deserialize<DateTime?>("\"  \"", Dates));
    }

    [Fact]
    public void DateTime_CadenaNoParseable_Lanza()
    {
        Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<DateTime>("\"no-fecha\"", Dates));
        Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<DateTime?>("\"no-fecha\"", Dates));
    }
}
