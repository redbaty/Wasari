using Microsoft.Extensions.DependencyInjection;
using Wasari.App.Abstractions;

namespace Wasari.App;

public record DownloadOptions
{
    public string? DefaultOutputDirectory { get; set; }

    public bool IncludeDubs { get; set; }

    public bool IncludeSubs { get; set; }

    public bool SkipExistingFiles { get; set; }

    public bool CreateSeriesFolder { get; set; }

    public bool CreateSeasonFolder { get; set; }

    public bool TryEnrichEpisodes { get; set; } = true;

    public bool OnlyDownloadEnrichedEpisodes { get; set; }

    public bool SkipUniqueEpisodeCheck { get; set; }

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
        if (HostDownloadService.TryGetValue(host, out var downloadServiceType)) return (IDownloadService)serviceProvider.GetRequiredService(downloadServiceType);

        return serviceProvider.GetRequiredService<GenericDownloadService>();
    }
}