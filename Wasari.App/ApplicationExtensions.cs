using Microsoft.Extensions.DependencyInjection;

namespace Wasari.App;

public static class ApplicationExtensions
{
    public static IServiceCollection AddDownloadModifier<T>(this IServiceCollection serviceCollection, string extractorKey) where T : class, IDownloadModifier
    {
        serviceCollection.Configure<DownloadOptions>(o =>
        {
            o.Modifiers.Add(extractorKey, typeof(T));
        });
        serviceCollection.AddScoped<T>();
        
        return serviceCollection;
    }
}