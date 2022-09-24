using Microsoft.Extensions.Options;
using Wasari.App.Abstractions;

namespace Wasari.App;

public class DownloadServiceSolver : IDownloadService
{
    public DownloadServiceSolver(IOptions<DownloadOptions> options, IServiceProvider serviceProvider)
    {
        Options = options;
        ServiceProvider = serviceProvider;
    }

    private IOptions<DownloadOptions> Options { get; }
    
    private IServiceProvider ServiceProvider { get; }
    
    public Task<DownloadedEpisode[]> DownloadEpisodes(string url, int levelOfParallelism)
    {
        var uri = new Uri(url);
        var downloadService = Options.Value.GetDownloader(uri.Host, ServiceProvider);
        return downloadService.DownloadEpisodes(url, levelOfParallelism);
    }
}