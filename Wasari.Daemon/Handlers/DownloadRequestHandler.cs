using Microsoft.Extensions.Options;
using Wasari.App;
using Wasari.App.Abstractions;
using Wasari.Daemon.Models;
using Wasari.Daemon.Options;
using Wolverine;

namespace Wasari.Daemon.Handlers;

public class DownloadRequestHandler
{
    public async ValueTask Handle(DownloadRequest request,
        ILogger<DownloadRequestHandler> logger,
        DownloadServiceSolver downloadServiceSolver,
        IServiceProvider serviceProvider,
        IOptions<NotificationOptions> notificationOptions,
        IOptions<DownloadOptions> downloadOptions)
    {
        logger.LogInformation("Starting download of {Url}", request.Url);

        var downloadService = downloadServiceSolver.GetService(request.Url);
        var episodesRange = new Ranges(request.EpisodeNumber, request.EpisodeNumber);
        var seasonsRange = new Ranges(request.SeasonNumber, request.SeasonNumber);

        var outputDirectoryOverride = string.IsNullOrEmpty(request.SeriesNameOverride) ? null : string.IsNullOrEmpty(downloadOptions.Value.DefaultOutputDirectory) ? null : Path.Combine(downloadOptions.Value.DefaultOutputDirectory, request.SeriesNameOverride);
        var episodes = await downloadService.DownloadEpisodes(request.Url.ToString(), 1,
            new DownloadEpisodeOptions(episodesRange, seasonsRange, outputDirectoryOverride));

        foreach (var downloadedEpisode in episodes)
        {
            switch (downloadedEpisode.Status)
            {
                case DownloadedEpisodeStatus.Downloaded:
                    logger.LogInformation("Downloaded {Episode}", downloadedEpisode);
                    break;
                case DownloadedEpisodeStatus.AlreadyExists:
                    logger.LogWarning("Episode already exists {Episode}", downloadedEpisode);
                    break;
                case DownloadedEpisodeStatus.Failed:
                    logger.LogError("Failed to download {Episode}", downloadedEpisode);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        if (notificationOptions.Value.Enabled && serviceProvider.GetService<NotificationService>() is { } notificationService)
        {
            await notificationService.SendNotifcationForDownloadedEpisodeAsync(episodes);
        }
    }
}