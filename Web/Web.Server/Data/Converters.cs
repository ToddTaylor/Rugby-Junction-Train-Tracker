using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Globalization;

namespace Web.Server.Data
{
    public static class Converters
    {
        public static readonly ValueConverter<DateTime, string> UtcDateTimeConverter =
            new ValueConverter<DateTime, string>(
                v => v.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                v => DateTime.Parse(v, null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)
            );
    }
}
