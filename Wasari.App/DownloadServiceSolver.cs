using Microsoft.Extensions.Options;
using Wasari.App.Abstractions;

namespace Wasari.App;

public class DownloadServiceSolver
{
    public DownloadServiceSolver(IOptions<DownloadOptions> options, IServiceProvider serviceProvider)
    {
        Options = options;
        ServiceProvider = serviceProvider;
    }

    private IOptions<DownloadOptions> Options { get; }

    private IServiceProvider ServiceProvider { get; }

    public IDownloadService GetService(Uri uri)
    {
        return Options.Value.GetDownloader(uri.Host, ServiceProvider);
    }
}