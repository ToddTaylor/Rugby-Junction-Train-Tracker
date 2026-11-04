using Services.Models;
using System.Globalization;
using System.Text;

namespace Services.Diagnostics
{
    /// <summary>
    /// Parses the output of "schtasks /query /tn &lt;name&gt; /v /fo csv".
    /// </summary>
    public static class ScheduledTaskCsvParser
    {
        private const int DefaultLastRunTimeColumn = 5;
        private const int DefaultLastResultColumn = 6;

        public static ScheduledTaskStatus Parse(string taskName, string csv, CultureInfo? culture = null, TimeZoneInfo? timeZone = null)
        {
            var status = new ScheduledTaskStatus { Name = taskName };

            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length < 2)
            {
                return status;
            }

            var header = SplitCsvLine(lines[0]);
            var row = SplitCsvLine(lines[1]);

            var lastRunColumn = IndexOf(header, "Last Run Time", DefaultLastRunTimeColumn);
            var lastResultColumn = IndexOf(header, "Last Result", DefaultLastResultColumn);

            status.Found = true;

            if (lastRunColumn < row.Count &&
                DateTime.TryParse(row[lastRunColumn], culture ?? CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var lastRunLocal) &&
                lastRunLocal.Year >= 2000) // Tasks that never ran report 11/30/1999.
            {
                var unspecified = DateTime.SpecifyKind(lastRunLocal, DateTimeKind.Unspecified);
                status.LastRunAt = TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZone ?? TimeZoneInfo.Local);
            }

            if (lastResultColumn < row.Count &&
                int.TryParse(row[lastResultColumn], NumberStyles.Integer, CultureInfo.InvariantCulture, out var lastResult))
            {
                status.LastResult = lastResult;
            }

            return status;
        }

        private static int IndexOf(List<string> header, string columnName, int fallback)
        {
            var index = header.FindIndex(h => string.Equals(h, columnName, StringComparison.OrdinalIgnoreCase));
            return index >= 0 ? index : fallback;
        }

        private static List<string> SplitCsvLine(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            fields.Add(current.ToString());
            return fields;
        }
    }
}
