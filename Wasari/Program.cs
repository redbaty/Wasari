using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CliFx;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Wasari.App;
using Wasari.Commands;
using Wasari.Crunchyroll;
using Wasari.Models;
using Wasari.ProgressSink;
using WasariEnvironment;

namespace Wasari
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            var loggerConfiguration = new LoggerConfiguration();

            var useProgressBar = Environment.GetEnvironmentVariable("NO_PROGRESS_BAR") == null && Environment.UserInteractive && args.All(i => i != "-np");

            if (useProgressBar) Console.CursorVisible = false;

            try
            {
                loggerConfiguration = loggerConfiguration
                    .MinimumLevel.Debug()
                    .WriteTo.ProgressConsole(enableProgressBars: useProgressBar);

                var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Wasari", "logs", "log.txt");
                
                Log.Logger = loggerConfiguration
                    .WriteTo.File(
                        logPath, rollingInterval: RollingInterval.Day,
                        restrictedToMinimumLevel: LogEventLevel.Verbose)
                    .CreateLogger();

                if (!useProgressBar)
                    Log.Logger.Warning("Progress bars disabled");
                
                Log.Logger.Information("Logging to file at path {@Path}", logPath);

                var serviceCollection = new ServiceCollection();
                serviceCollection.AddTransient<CrunchyRollAuthenticationService>();
                serviceCollection.AddTransient<CrunchyrollDownloadSeriesCommand>();
                serviceCollection.AddTransient<CrunchyrollListSeriesCommand>();
                serviceCollection.AddLogging(c => c.AddSerilog());
                serviceCollection.Configure<ProgressBarOptions>(o => o.Enabled = true);
                serviceCollection.AddMemoryCache();
                serviceCollection.AddDownloadServices();
                await serviceCollection.AddEnvironmentServices();
                serviceCollection.AddCrunchyrollServices();

                await using var serviceProvider = serviceCollection.BuildServiceProvider();
                var environmentOptions = serviceProvider.GetService<IOptions<EnvironmentOptions>>();
                if (environmentOptions?.Value?.Features is { } features)
                {
                    Log.Logger.Information("Available environment features: {@Features}", features.Select(i => i.Type));
                }

                if (Assembly.GetEntryAssembly()?.GetName()?.Version is { } version)
                {
                    Log.Logger.Information("Current version is: {@Version}", version.ToString());
                }

                return await new CliApplicationBuilder()
                    .AddCommand<CrunchyrollDownloadSeriesCommand>()
                    .AddCommand<CrunchyrollListSeriesCommand>()
                    .UseTypeActivator(serviceProvider.GetService)
                    .Build()
                    .RunAsync(args.Where(i => i != "-np").ToArray());
            }
            finally
            {
                if (useProgressBar) Console.CursorVisible = true;
            }
        }
    }
}