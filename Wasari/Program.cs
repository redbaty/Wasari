using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
using WasariEnvironment;

namespace Wasari
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            Console.CursorVisible = false;
            var loggerConfiguration = new LoggerConfiguration();

            var konsoleAvailable =
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && KonsoleSink.AvailableHeight > 10 && args.All(i => i != "-nk");

            loggerConfiguration = konsoleAvailable
                ? loggerConfiguration.WriteTo.Sink<KonsoleSink>()
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

            if (!konsoleAvailable)
                Log.Logger.Warning("Konsole isn't available, falling back to regular Console sink");
            
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddTransient<CrunchyRollAuthenticationService>();
            serviceCollection.AddTransient<CrunchyrollDownloadSeriesCommand>();
            serviceCollection.AddTransient<CrunchyrollListSeriesCommand>();
            serviceCollection.AddLogging(c => c.AddSerilog());
            serviceCollection.Configure<ProgressBarOptions>(o => o.Enabled = true);
            await serviceCollection.AddEnvironmentServices();
            await serviceCollection.AddCrunchyrollServices();

            await using var serviceProvider = serviceCollection.BuildServiceProvider();
            var environmentOptions = serviceProvider.GetService<IOptions<EnvironmentOptions>>();
            if (environmentOptions?.Value?.Features is { } features)
            {
                Log.Logger.Information("Available environment features: {@Features}", features);
            }

            return await new CliApplicationBuilder()
                .AddCommand<CrunchyrollDownloadSeriesCommand>()
                .AddCommand<CrunchyrollListSeriesCommand>()
                .UseTypeActivator(serviceProvider.GetService)
                .Build()
                .RunAsync(args.Where(i => i != "-nk").ToArray());
        }
    }
}