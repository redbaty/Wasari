using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Wasari.Cli;

internal static class ServicesExtensions
{
    public static IServiceCollection AddRootServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddLogging(c => c.AddSerilog());
        return serviceCollection;
    }
}