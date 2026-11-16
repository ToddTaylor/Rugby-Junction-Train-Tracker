using System.Globalization;
using System.Text.RegularExpressions;

namespace Services.Deserializers
{
    public class DeserializerBase
    {
        // SoftEOT and SoftDPU write the same format whatever the PC's regional settings. Parsing with the
        // current culture read "/" and ":" as that culture's separators and "89.5" as 895 where the decimal
        // separator is a comma, so the invariant culture is used throughout.

        /// <summary>Reads a log timestamp, which is in the PC's local time, and returns it in UTC.</summary>
        protected static DateTime ConvertToDateTimeUTC(string input)
        {
            return DateTime.ParseExact(input, "yyyy/MM/dd-HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal)
                .ToUniversalTime();
        }

        protected static decimal? ConvertToDecimal(string input)
        {
            return decimal.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal output) ? output : null;
        }

        protected static int? ConvertToInteger(string input)
        {
            return int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int output) ? output : null;
        }

        protected static string? ConvertToNullIfDash(string input)
        {
            return string.IsNullOrWhiteSpace(input.Replace("-", "")) ? null : input;
        }

        protected static string ReplaceMultipleSpaces(string data)
        {
            return Regex.Replace(data, @"\s+", " ");
        }

        protected static string[] SplitIntoToArrayBySpaces(string data)
        {
            return data.Split(' ', StringSplitOptions.TrimEntries);
        }
    }
}