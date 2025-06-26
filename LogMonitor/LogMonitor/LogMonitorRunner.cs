using LogMonitor.MonitorUtils;
using LogMonitorData.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogMonitor
{
    //encapsulates all of the work currently in Program.cs
    public class LogMonitorRunner
    {
        private readonly IConfiguration _config;
        private readonly ILogParserService _parser;
        private readonly IJobMonitorService _monitor;
        private readonly ILogger<LogMonitorRunner> _logger;

        public LogMonitorRunner(
            IConfiguration config,
            ILogParserService parser,
            IJobMonitorService monitor,
            ILogger<LogMonitorRunner> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public int Run(TextWriter output)
        {
            var settings = _config.GetSection("LogMonitor").Get<LogMonitorSettings>()
                         ?? throw new InvalidOperationException("Missing settings");

            try
            {
                //Determine log-file path and ensure folder exists
                var folder = Path.IsPathRooted(settings.LogsFolder)
                    ? settings.LogsFolder
                    : Path.Combine(AppContext.BaseDirectory, settings.LogsFolder);
                Directory.CreateDirectory(folder);

                var logPath = Path.Combine(folder, settings.LogFileName);
                _logger.LogInformation($"Reading log from {logPath}");

                //Parse and analyze
                var entries = _parser.ParseLogEntries(logPath).ToList();
                _logger.LogInformation($"Parsed {entries.Count} entries");

                var jobs = _monitor.AnalyzeJobs(entries).ToList();
                _logger.LogInformation($"Analyzed {jobs.Count} jobs");

                //Print report
                Console.WriteLine("Job Report");
                foreach (var job in jobs)
                {
                    Console.WriteLine($"PID: {job.PID} with Description: {job.Description} - ran for: {job.Duration:mm\\:ss} (from {job.StartTime:c} to {job.EndTime:c})");
                }
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Fatal error");
                return 1;
            }
        }
    }
}
