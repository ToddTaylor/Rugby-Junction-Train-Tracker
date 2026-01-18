using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace Web.Server.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class LogsController : ControllerBase
    {
        private readonly ILogger<LogsController> _logger;
        private readonly string _logsDirectory;

        public LogsController(ILogger<LogsController> logger)
        {
            _logger = logger;
            _logsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "..\\logs");
        }

        /// <summary>
        /// Gets the latest log entries from the current day's log file.
        /// </summary>
        /// <param name="lines">Number of lines to retrieve from the end of the file (default: 100, max: 1000)</param>
        /// <returns>Log entries as plain text</returns>
        [HttpGet("latest")]
        public async Task<IActionResult> GetLatestLogs([FromQuery] int lines = 100)
        {
            try
            {
                // Limit the number of lines
                lines = Math.Min(Math.Max(lines, 1), 1000);

                if (!Directory.Exists(_logsDirectory))
                {
                    return NotFound(new { error = "Logs directory not found." });
                }

                // Get today's log file
                var today = DateTime.Now.ToString("yyyyMMdd");
                var logFileName = $"web-server-{today}.log";
                var logFilePath = Path.Combine(_logsDirectory, logFileName);

                if (!System.IO.File.Exists(logFilePath))
                {
                    // Try to get the most recent log file
                    var logFiles = Directory.GetFiles(_logsDirectory, "web-server-*.log")
                        .OrderByDescending(f => System.IO.File.GetLastWriteTime(f))
                        .ToArray();

                    if (logFiles.Length == 0)
                    {
                        return Ok(new { message = "No log files found.", logs = "" });
                    }

                    logFilePath = logFiles[0];
                }

                var logLines = await ReadLastLinesAsync(logFilePath, lines);

                return Ok(new
                {
                    fileName = Path.GetFileName(logFilePath),
                    lineCount = logLines.Count,
                    logs = string.Join(Environment.NewLine, logLines)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving latest logs.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred while retrieving logs." });
            }
        }

        /// <summary>
        /// Gets a list of available log files.
        /// </summary>
        /// <returns>List of log file names with their sizes and last modified dates</returns>
        [HttpGet("files")]
        public IActionResult GetLogFiles()
        {
            try
            {
                if (!Directory.Exists(_logsDirectory))
                {
                    return NotFound(new { error = "Logs directory not found." });
                }

                var logFiles = Directory.GetFiles(_logsDirectory, "web-server-*.log")
                    .Select(filePath => new
                    {
                        fileName = Path.GetFileName(filePath),
                        sizeBytes = new FileInfo(filePath).Length,
                        lastModified = System.IO.File.GetLastWriteTime(filePath).ToString("O")
                    })
                    .OrderByDescending(f => f.lastModified)
                    .ToList();

                return Ok(new { files = logFiles });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving log file list.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred while retrieving log files." });
            }
        }

        /// <summary>
        /// Gets the contents of a specific log file.
        /// </summary>
        /// <param name="fileName">The name of the log file (e.g., web-server-20260108.log)</param>
        /// <param name="lines">Number of lines to retrieve from the end of the file (default: all, max: 10000)</param>
        /// <returns>Log file contents as plain text</returns>
        [HttpGet("file/{fileName}")]
        public async Task<IActionResult> GetLogFile(string fileName, [FromQuery] int? lines = null)
        {
            try
            {
                // Validate filename to prevent directory traversal attacks
                if (string.IsNullOrWhiteSpace(fileName) ||
                    fileName.Contains("..") ||
                    fileName.Contains("/") ||
                    fileName.Contains("\\") ||
                    !fileName.StartsWith("web-server-") ||
                    !fileName.EndsWith(".log"))
                {
                    return BadRequest(new { error = "Invalid file name." });
                }

                if (!Directory.Exists(_logsDirectory))
                {
                    return NotFound(new { error = "Logs directory not found." });
                }

                var logFilePath = Path.Combine(_logsDirectory, fileName);

                if (!System.IO.File.Exists(logFilePath))
                {
                    return NotFound(new { error = "Log file not found." });
                }

                List<string> logLines;

                if (lines.HasValue)
                {
                    // Limit the number of lines
                    var maxLines = Math.Min(Math.Max(lines.Value, 1), 10000);
                    logLines = await ReadLastLinesAsync(logFilePath, maxLines);
                }
                else
                {
                    // Read all lines (with reasonable limit) using shared file access
                    logLines = await ReadAllLinesWithSharedAccessAsync(logFilePath);
                    if (logLines.Count > 10000)
                    {
                        logLines = logLines.Skip(logLines.Count - 10000).ToList();
                    }
                }

                return Ok(new
                {
                    fileName = fileName,
                    lineCount = logLines.Count,
                    logs = string.Join(Environment.NewLine, logLines)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving log file {FileName}.", fileName);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred while retrieving the log file." });
            }
        }

        /// <summary>
        /// Searches log files for entries containing the specified text.
        /// </summary>
        /// <param name="query">The text to search for</param>
        /// <param name="maxResults">Maximum number of results to return (default: 100, max: 500)</param>
        /// <returns>Matching log entries</returns>
        [HttpGet("search")]
        public async Task<IActionResult> SearchLogs([FromQuery] string query, [FromQuery] int maxResults = 100)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest(new { error = "Search query is required." });
                }

                maxResults = Math.Min(Math.Max(maxResults, 1), 500);

                if (!Directory.Exists(_logsDirectory))
                {
                    return NotFound(new { error = "Logs directory not found." });
                }

                var logFiles = Directory.GetFiles(_logsDirectory, "web-server-*.log")
                    .OrderByDescending(f => System.IO.File.GetLastWriteTime(f))
                    .Take(7) // Search last 7 days
                    .ToArray();

                var matchingLines = new List<string>();

                foreach (var logFile in logFiles)
                {
                    var lines = await ReadAllLinesWithSharedAccessAsync(logFile);
                    var matches = lines.Where(line => line.Contains(query, StringComparison.OrdinalIgnoreCase));

                    foreach (var match in matches)
                    {
                        matchingLines.Add($"[{Path.GetFileName(logFile)}] {match}");

                        if (matchingLines.Count >= maxResults)
                        {
                            break;
                        }
                    }

                    if (matchingLines.Count >= maxResults)
                    {
                        break;
                    }
                }

                return Ok(new
                {
                    query = query,
                    resultCount = matchingLines.Count,
                    results = matchingLines
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while searching logs for query: {Query}.", query);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred while searching logs." });
            }
        }

        /// <summary>
        /// Helper method to read all lines from a file with shared access.
        /// </summary>
        private async Task<List<string>> ReadAllLinesWithSharedAccessAsync(string filePath)
        {
            var lines = new List<string>();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (line != null)
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

        /// <summary>
        /// Helper method to read the last N lines from a file efficiently.
        /// </summary>
        private async Task<List<string>> ReadLastLinesAsync(string filePath, int lineCount)
        {
            var lines = new List<string>();

            try
            {
                // For smaller files or small line counts, just read all and take last N
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length < 1024 * 1024 || lineCount < 100) // < 1MB or < 100 lines
                {
                    var allLines = await ReadAllLinesWithSharedAccessAsync(filePath);
                    return allLines.Skip(Math.Max(0, allLines.Count - lineCount)).ToList();
                }

                // For larger files, read from the end
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                var buffer = new List<string>();
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (line != null)
                    {
                        buffer.Add(line);
                        if (buffer.Count > lineCount)
                        {
                            buffer.RemoveAt(0);
                        }
                    }
                }

                return buffer;
            }
            catch
            {
                // Fallback: use shared access read
                var allLines = await ReadAllLinesWithSharedAccessAsync(filePath);
                return allLines.Skip(Math.Max(0, allLines.Count - lineCount)).ToList();
            }
        }
    }
}
