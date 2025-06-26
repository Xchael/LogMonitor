using LogMonitorData.Models;
using LogMonitorData.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogMonitorData.Services
{
    /// <summary>
    /// Service responsible for reading and parsing log entries from a CSV-formatted log file.
    /// Implements ILogParserService to allow for dependency injection and testability.
    /// </summary>
    public class LogParserService : ILogParserService
    {
        private readonly ILogger<LogParserService> _logger;

        public LogParserService(ILogger<LogParserService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }


        /// <summary>
        /// Reads the specified log file line by line, parses each CSV entry
        /// into a LogEntry model, and yields the parsed entries.
        /// </summary>
        /// <param name="filePath">The full path to the log file to parse.</param>
        public IEnumerable<LogEntry> ParseLogEntries(string filePath)
        {
            //Validate path
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path must be provided.", nameof(filePath));

            // Check that the file actually exists before attempting to open it
            if (!File.Exists(filePath))
            {
                _logger.LogError($"Log file not found: {filePath}");
                throw new FileNotFoundException("Log file not found.", filePath);
            }

            _logger.LogInformation($"Starting to parse log file: {filePath}");

            // Read the file lazily, one line at a time
            foreach (var line in File.ReadLines(filePath))
            {
                // Skip blank lines
                if (string.IsNullOrWhiteSpace(line))
                {
                    _logger.LogWarning("Skipping empty line.");
                    continue;
                }

                string[] parts;
                try
                {
                    parts = line.Split(',', 4);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to split line: {line}");
                    continue;
                }

                if (parts.Length != 4)
                {
                    _logger.LogWarning($"Invalid log format (expected 4 parts): {line}");
                    continue;
                }

                // Timestamp 
                if (!TimeSpan.TryParseExact(parts[0], "c", CultureInfo.InvariantCulture, out var ts))
                {
                    _logger.LogWarning($"Invalid timestamp '{parts[0]}' in line: {line}");
                    continue;
                }

                var timestamp = ts;

                // Job Description 
                var description = parts[1].Trim();
                if (string.IsNullOrWhiteSpace(description))
                {
                    //try to log empty description for further use
                    _logger.LogWarning($"Empty job description in line: {line}");
                }

                // START/END Marker
                var marker = parts[2].Trim();
                bool isStart;
                if (marker.Equals("START", StringComparison.OrdinalIgnoreCase))
                {
                    isStart = true;
                }
                else if (marker.Equals("END", StringComparison.OrdinalIgnoreCase))
                {
                    isStart = false;
                }
                else
                {
                    _logger.LogWarning($"Unknown marker '{marker}' in line: {line}");
                    continue;
                }

                // PID 
                if (!int.TryParse(parts[3].Trim(), out var pid))
                {
                    _logger.LogWarning($"Invalid PID '{parts[3]}' in line: {line}");
                    continue;
                }

                // Emit the parsed LogEntry
                yield return new LogEntry
                {
                    TimeStamp = timestamp,
                    Pid = pid,
                    JobDescription = description,
                    IsStart = isStart
                };
            }

            _logger.LogInformation("Completed parsing log file: {FilePath}", filePath);
        }
    }
}
