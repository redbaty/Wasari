using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WasariEnvironment;

namespace Wasari.Cli;

internal static class ServicesExtensions
{
    public static async Task<IServiceCollection> AddRootServices(this IServiceCollection serviceCollection)
    {
        await serviceCollection.AddEnvironmentServices();
        serviceCollection.AddLogging(c => c.AddSerilog());
        return serviceCollection;
    }
}