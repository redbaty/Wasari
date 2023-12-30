using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WasariEnvironment;
using WasariEnvironment.Extensions;

namespace Wasari.Cli;

internal static class ServicesExtensions
{
    public static async Task<IServiceCollection> AddRootServices(this IServiceCollection serviceCollection)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(new EnvironmentVariableLoggingLevelSwitch("%LOG_LEVEL%"))
            .WriteTo.Console(outputTemplate: "[{Timestamp:u} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        await serviceCollection.AddEnvironmentServices();
        serviceCollection.AddLogging(c => c.AddSerilog());
        return serviceCollection;
    }
}