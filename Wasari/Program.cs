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
using Wasari.Sinks;
using WasariEnvironment;

namespace Wasari
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            var loggerConfiguration = new LoggerConfiguration();

            var useProgressBar = args.All(i => i != "-np");

            if (useProgressBar) Console.CursorVisible = false;

            try
            {
                loggerConfiguration = useProgressBar
                    ? loggerConfiguration.WriteTo.Sink<ProgressSink>()
                    : loggerConfiguration.WriteTo.Console()
                        .Filter
                        .ByIncludingOnly(i =>
                            i.Level != LogEventLevel.Information ||
                            !i.MessageTemplate.Text.StartsWith("[Progress Update]"));

                Log.Logger = loggerConfiguration
                    .WriteTo.File(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "Wasari", "logs", "log.txt"), rollingInterval: RollingInterval.Day,
                        restrictedToMinimumLevel: LogEventLevel.Verbose)
                    .CreateLogger();

                if (!useProgressBar)
                    Log.Logger.Warning("Progress bars disabled");

                var serviceCollection = new ServiceCollection();
                serviceCollection.AddTransient<CrunchyRollAuthenticationService>();
                serviceCollection.AddTransient<CrunchyrollDownloadSeriesCommand>();
                serviceCollection.AddTransient<CrunchyrollListSeriesCommand>();
                serviceCollection.AddLogging(c => c.AddSerilog());
                serviceCollection.Configure<ProgressBarOptions>(o => o.Enabled = true);
                serviceCollection.AddMemoryCache();
                await serviceCollection.AddEnvironmentServices();
                serviceCollection.AddCrunchyrollServices();

                await using var serviceProvider = serviceCollection.BuildServiceProvider();
                var environmentOptions = serviceProvider.GetService<IOptions<EnvironmentOptions>>();
                if (environmentOptions?.Value?.Features is { } features)
                {
                    Log.Logger.Information("Available environment features: {@Features}", features);
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