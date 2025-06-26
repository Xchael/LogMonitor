using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LogMonitorData.Services;
using LogMonitorData.Models;
using Microsoft.Extensions.Configuration;
using LogMonitor.MonitorUtils;
using LogMonitorData.Services.Interfaces;
using System.Threading;



//Build a host to get logging and dep injection support
var host = Host.CreateDefaultBuilder()
    .ConfigureAppConfiguration((hostingCtx, config) =>
    {
        // Ensure we read our appsettings.json from the output folder
        config.SetBasePath(AppContext.BaseDirectory)
              .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    })
    .ConfigureLogging(logging =>
    {
        // Clear default providers, add Console + Debug
        logging.ClearProviders();
        logging.AddConsole();
        logging.AddDebug();
    })
    .ConfigureServices((hostingCtx, services) =>
    {
        // Bind our settings POCO to the "LogMonitor" section
        services.Configure<LogMonitorSettings>(
            hostingCtx.Configuration.GetSection("LogMonitor"));

        // Register the core services from LogMonitorData
        services.AddSingleton<ILogParserService, LogParserService>();
        services.AddSingleton<IJobMonitorService, JobMonitorService>();
    })
    .Build();

//Create a scope for resolving scoped services
using var scope = host.Services.CreateScope();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
var settings = config.GetSection("LogMonitor").Get<LogMonitorSettings>()
                         ?? throw new InvalidOperationException("Missing settings");
var parser = scope.ServiceProvider.GetRequiredService<ILogParserService>();
var monitor = scope.ServiceProvider.GetRequiredService<IJobMonitorService>();


try
{
    //Determine log-file path and ensure folder exists
    var folder = Path.IsPathRooted(settings.LogsFolder)
        ? settings.LogsFolder
        : Path.Combine(AppContext.BaseDirectory, settings.LogsFolder);
    Directory.CreateDirectory(folder);

    var logPath = Path.Combine(folder, settings.LogFileName);
    logger.LogInformation($"Reading log from {logPath}");

    //Parse and analyze
    var entries = parser.ParseLogEntries(logPath).ToList();
    logger.LogInformation($"Parsed {entries.Count} entries");

    var jobs = monitor.AnalyzeJobs(entries).ToList();
    logger.LogInformation($"Analyzed {jobs.Count} jobs");

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
    logger.LogCritical(ex, "Fatal error");
    return 1;
}

