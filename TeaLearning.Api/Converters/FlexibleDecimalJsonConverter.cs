using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TeaLearning.Api.Converters;

/// <summary>
/// Permite deserializar valores decimales enviados como número JSON (4.5) o cadena con punto o coma ("4.5" / "4,5")
/// de forma independiente del locale del sistema operativo (Azure Linux/Windows o entorno local).
/// </summary>
public sealed class FlexibleDecimalJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetDecimal(out var numericValue))
        {
            return numericValue;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var raw = reader.GetString()?.Trim().Replace(',', '.');
            if (!string.IsNullOrEmpty(raw) &&
                decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedValue))
            {
                return parsedValue;
            }
        }

        throw new JsonException("El valor numérico decimal no es válido. Use punto o coma decimal (por ejemplo: 4.5 o 4,5).");
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

public sealed class FlexibleNullableDecimalJsonConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetDecimal(out var numericValue))
        {
            return numericValue;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var raw = reader.GetString()?.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            var normalized = raw.Replace(',', '.');
            if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedValue))
            {
                return parsedValue;
            }
        }

        throw new JsonException("El valor numérico decimal no es válido. Use punto o coma decimal (por ejemplo: 4.5 o 4,5).");
    }

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteNumberValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
