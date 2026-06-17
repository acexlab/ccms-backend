using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ccms_backend.services;

public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetDateTime().ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(
            (value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value)
            .ToUniversalTime()
            .ToString("yyyy-MM-ddTHH:mm:ssZ")
        );
    }
}
