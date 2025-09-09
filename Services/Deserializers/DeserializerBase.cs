using System.Text.RegularExpressions;

namespace Services.Deserializers
{
    public class DeserializerBase
    {
        protected static DateTime ConvertToDateTimeUTC(string input)
        {
            return DateTime.ParseExact(input, "yyyy/MM/dd-HH:mm:ss", null).ToUniversalTime();
        }

        protected static decimal? ConvertToDecimal(string input)
        {
            return decimal.TryParse(input, out decimal output) ? output : null;
        }

        protected static int? ConvertToInteger(string input)
        {
            return int.TryParse(input, out int output) ? output : null;
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