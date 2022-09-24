using Microsoft.Extensions.DependencyInjection;

namespace Wasari.App;

public record Range(int? Minimum, int? Maximum);

public record DownloadOptions
{
    public string? OutputDirectory { get; set; }
    
    public bool IncludeDubs { get; set; }
    
    public bool IncludeSubs { get; set; }
    
    public bool SkipExistingFiles { get; set; }
    
    public Range? EpisodesRange { get; set; }
    
    public Range? SeasonsRange { get; set; }

    internal Dictionary<string, Type> Modifiers { get; } = new();
    
    public bool CreateSeriesFolder { get; set; }
    
    public bool CreateSeasonFolder { get; set; }

    private Dictionary<string, Type> HostDownloadService { get; } = new();

    public DownloadOptions AddHostDownloader<T>(string host) where T : IDownloadService
    {
        if (string.IsNullOrEmpty(host))
            throw new ArgumentNullException(nameof(host));
        
        HostDownloadService.Add(host.ToLowerInvariant().Trim(), typeof(T));
        return this;
    }

    public IDownloadService GetDownloader(string host, IServiceProvider serviceProvider)
    {
        if (HostDownloadService.TryGetValue(host, out var downloadServiceType))
        {
            return (IDownloadService)serviceProvider.GetRequiredService(downloadServiceType);
        }

        return serviceProvider.GetRequiredService<GenericDownloadService>();
    }
}