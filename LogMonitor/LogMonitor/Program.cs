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
using LogMonitor;



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
        //no point in interface for now
        services.AddSingleton<LogMonitorRunner>();
    })
    .Build();

//Create a scope for resolving scoped services
using var scope = host.Services.CreateScope();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
var parser = scope.ServiceProvider.GetRequiredService<ILogParserService>();
var monitor = scope.ServiceProvider.GetRequiredService<IJobMonitorService>();

//for testing purposes
var runner = scope.ServiceProvider.GetRequiredService<LogMonitorRunner>();

return runner.Run(Console.Out);
