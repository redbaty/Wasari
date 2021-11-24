using System;
using System.IO;
using System.Threading.Tasks;
using CliFx;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Wasari.App;
using Wasari.Commands;
using Wasari.Crunchyroll;
using Wasari.Models;

namespace Wasari
{
    internal static class Program
    {
        private static async Task<int> Main()
        {
            Console.CursorVisible = false;
            var loggerConfiguration = new LoggerConfiguration();

            var konsoleAvailable = KonsoleSink.AvailableHeight > 10;
            loggerConfiguration = konsoleAvailable
                ? loggerConfiguration.WriteTo.Sink<KonsoleSink>()
                : loggerConfiguration.WriteTo.Console();

            Log.Logger = loggerConfiguration
                .WriteTo.File(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Wasari", "logs", "log.txt"), rollingInterval: RollingInterval.Day,
                    restrictedToMinimumLevel: LogEventLevel.Verbose)
                .CreateLogger();
            
            if(!konsoleAvailable)
                Log.Logger.Warning("There isn't enough space available for Konsole, falling back to regular Console sink");

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddTransient<CrunchyRollAuthenticationService>();
            serviceCollection.AddTransient<CrunchyrollDownloadSeriesCommand>();
            serviceCollection.AddLogging(c => c.AddSerilog());
            serviceCollection.Configure<ProgressBarOptions>(o => { o.Enabled = true; });
            await serviceCollection.AddCrunchyrollServices();
            var serviceProvider = serviceCollection.BuildServiceProvider();

            return await new CliApplicationBuilder()
                .AddCommand<CrunchyrollDownloadSeriesCommand>()
                .UseTypeActivator(serviceProvider.GetService)
                .Build()
                .RunAsync();
        }
    }
}