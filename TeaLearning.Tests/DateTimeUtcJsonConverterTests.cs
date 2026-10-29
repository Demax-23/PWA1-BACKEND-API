using System.Text.Json;
using TeaLearning.Api.Converters;
using TeaLearning.Services.DTOs.Users;

namespace TeaLearning.Tests;

public class DateTimeUtcJsonConverterTests
{
    private readonly JsonSerializerOptions _jsonOptions;

    public DateTimeUtcJsonConverterTests()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        _jsonOptions.Converters.Add(new UtcDateTimeJsonConverter());
        _jsonOptions.Converters.Add(new NullableUtcDateTimeJsonConverter());
    }

    [Fact]
    public void UtcDateTimeJsonConverter_ShouldAppendZSuffix_WhenSerializingUtcDateTime()
    {
        // Arrange: Unspecified or UTC DateTime
        var testDate = new DateTime(2026, 10, 29, 1, 16, 0, DateTimeKind.Unspecified);

        // Act
        var json = JsonSerializer.Serialize(testDate, _jsonOptions);

        // Assert: Must include the 'Z' indicating UTC timezone
        Assert.Equal("\"2026-10-29T01:16:00.000Z\"", json);
    }

    [Fact]
    public void NullableUtcDateTimeJsonConverter_ShouldAppendZSuffix_WhenSerializingNullableDateTime()
    {
        // Arrange
        DateTime? testDate = new DateTime(2026, 10, 29, 1, 16, 0, DateTimeKind.Utc);

        // Act
        var json = JsonSerializer.Serialize(testDate, _jsonOptions);

        // Assert
        Assert.Equal("\"2026-10-29T01:16:00.000Z\"", json);
    }

    [Fact]
    public void NullableUtcDateTimeJsonConverter_ShouldSerializeNull_WhenValueIsNull()
    {
        // Arrange
        DateTime? testDate = null;

        // Act
        var json = JsonSerializer.Serialize(testDate, _jsonOptions);

        // Assert
        Assert.Equal("null", json);
    }

    [Fact]
    public void UserDto_Serialization_ShouldIncludeUtcDatesWithZSuffix()
    {
        // Arrange
        var userDto = new UserDto
        {
            Id = Guid.NewGuid(),
            Email = "admin@tealearning.com",
            Nombres = "Administrador",
            Apellidos = "Principal",
            CreatedAt = new DateTime(2026, 10, 28, 20, 0, 0, DateTimeKind.Utc),
            UltimaActividadAt = new DateTime(2026, 10, 29, 1, 16, 0, DateTimeKind.Utc)
        };

        // Act
        var json = JsonSerializer.Serialize(userDto, _jsonOptions);

        // Assert
        Assert.Contains("\"createdAt\":\"2026-10-28T20:00:00.000Z\"", json);
        Assert.Contains("\"ultimaActividadAt\":\"2026-10-29T01:16:00.000Z\"", json);
    }
}
