using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CliFx;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
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
        private static HashSet<string> ReservedArguments { get; } = new() { Wasari.ReservedArguments.NoProgressBar, Wasari.ReservedArguments.JsonOutput };

        private static async Task<int> Main(string[] args)
        {
            var loggerConfiguration = new LoggerConfiguration();

            var useProgressBar = Environment.GetEnvironmentVariable("NO_PROGRESS_BAR") == null && Environment.UserInteractive && args.All(i => i != Wasari.ReservedArguments.NoProgressBar);
            var useJsonOutput = Environment.GetEnvironmentVariable("JSON_OUTPUT") != null && args.Any(i => i == Wasari.ReservedArguments.JsonOutput);

            if (useProgressBar) Console.CursorVisible = false;

            try
            {
                var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Wasari", "logs", "log.txt");
                
                loggerConfiguration = loggerConfiguration
                    .MinimumLevel.Debug()
                    .WriteTo.File(logPath);

                loggerConfiguration = useJsonOutput ? loggerConfiguration.WriteTo.Console(new CompactJsonFormatter()) : loggerConfiguration.WriteTo.ProgressConsole(enableProgressBars: useProgressBar);

            

                Log.Logger = loggerConfiguration
                    .CreateLogger();

                if (!useProgressBar)
                    Log.Logger.Warning("Progress bars disabled");

                Log.Logger.Information("Logging to file at path {@Path}", logPath);

                var serviceCollection = new ServiceCollection();
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
                    .RunAsync(args.Where(i => !ReservedArguments.Contains(i)).ToArray());
            }
            finally
            {
                if (useProgressBar) Console.CursorVisible = true;
            }
        }
    }
}