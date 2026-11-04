using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Globalization;
using System.Text.Json;

namespace Web.Server.Data
{
    public static class Converters
    {
        public static readonly ValueConverter<DateTime, string> UtcDateTimeConverter =
            new ValueConverter<DateTime, string>(
                v => v.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                v => DateTime.Parse(v, null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)
            );

        public static readonly ValueConverter<DateTime?, string?> NullableUtcDateTimeConverter =
            new ValueConverter<DateTime?, string?>(
                v => v.HasValue ? v.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ") : null,
                v => string.IsNullOrWhiteSpace(v)
                    ? null
                    : DateTime.Parse(v, null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)
            );

        public static ValueConverter<List<T>, string> JsonListConverter<T>() =>
            new ValueConverter<List<T>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v)
                    ? new List<T>()
                    : JsonSerializer.Deserialize<List<T>>(v, (JsonSerializerOptions?)null) ?? new List<T>()
            );

        /// <summary>
        /// Compares JSON-stored lists by their serialized form so EF Core detects changes to list contents.
        /// </summary>
        public static ValueComparer<List<T>> JsonListComparer<T>() =>
            new ValueComparer<List<T>>(
                (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
                v => JsonSerializer.Deserialize<List<T>>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!
            );
    }
}
