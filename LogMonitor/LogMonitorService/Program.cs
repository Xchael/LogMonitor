using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.WindowsServices;
using LogMonitorData.Services;
using LogMonitorService.MonitorUtils;
using LogMonitorData.Services.Interfaces;
using LogMonitorService;

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService()

    //Load appsettings.json
    .ConfigureAppConfiguration(cfg =>
    {
        cfg.SetBasePath(AppContext.BaseDirectory)
           .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    })

    //Configure logging
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.AddEventLog();   
    })

    //Register DI services
    .ConfigureServices((ctx, services) =>
    {
        //bind settings
        services.Configure<LogMonitorSettings>(
            ctx.Configuration.GetSection("WorkerSettings"));
        services.Configure<LogMonitorSettings>(
            ctx.Configuration.GetSection("LogMonitor"));

        //core pipeline services
        services.AddSingleton<ILogParserService, LogParserService>();
        services.AddSingleton<IJobMonitorService, JobMonitorService>();

        //worker
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();