using LogMonitorData.Services.Interfaces;
using LogMonitorService.MonitorUtils;
using Microsoft.Extensions.Options;

namespace LogMonitorService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly ILogParserService _parser;
        private readonly IJobMonitorService _monitor;
        private readonly LogMonitorSettings _lmSettings;

        public Worker(ILogger<Worker> logger, 
            ILogParserService parser,
            IJobMonitorService monitor,
            IOptions<LogMonitorSettings> lmOpts)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _lmSettings = lmOpts?.Value ?? throw new ArgumentNullException(nameof(lmOpts));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var folder = Path.IsPathRooted(_lmSettings.LogsFolder)
                ? _lmSettings.LogsFolder
                : Path.Combine(AppContext.BaseDirectory, _lmSettings.LogsFolder);
            Directory.CreateDirectory(folder);
            var logFile = Path.Combine(folder, _lmSettings.LogFileName);

            // Create a periodic timer
            var interval = TimeSpan.FromHours(_lmSettings.IntervalHours);
            using var timer = new PeriodicTimer(interval);

            _logger.LogInformation($"LogMonitorWorker starting; running every {_lmSettings.IntervalHours} hours");

            // Run once immediately
            await RunIterationAsync(logFile, stoppingToken);

            // Then loop
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunIterationAsync(logFile, stoppingToken);
            }

            _logger.LogInformation("LogMonitorWorker stopping.");
        }

        private Task RunIterationAsync(string logFile, CancellationToken token)
        {
            try
            {
                _logger.LogInformation("Iteration started at {Time}", DateTime.Now);

                //Parse
                var entries = _parser
                    .ParseLogEntries(logFile)
                    .ToList();
                _logger.LogInformation("Parsed {Count} entries", entries.Count);

                //Analyze
                var jobs = _monitor
                    .AnalyzeJobs(entries)
                    .ToList();
                _logger.LogInformation("Analyzed {Count} jobs", jobs.Count);

                _logger.LogInformation("Iteration completed at {Time}", DateTime.Now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during iteration");
            }

            return Task.CompletedTask;
        }
    }
}
