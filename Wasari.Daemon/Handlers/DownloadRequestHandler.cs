using Medallion.Threading.Redis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wasari.App;
using Wasari.App.Abstractions;
using Wasari.Daemon.Models;
using Wasari.Daemon.Options;
using Wasari.FFmpeg;
using Wolverine;

namespace Wasari.Daemon.Handlers;

public class DownloadRequestHandler
{
    public async ValueTask Handle(DownloadRequest request,
        ILogger<DownloadRequestHandler> logger,
        IServiceProvider serviceProvider,
        IOptions<DaemonOptions> daemonOptions,
        IMessageBus messageBus)
    {
        if (daemonOptions.Value.RedisLockEnabled)
        {
            var connectionMultiplexer = serviceProvider.GetRequiredService<ConnectionMultiplexer>();
            var @lock = new RedisDistributedLock($"S{request.SeasonNumber:00}E{request.EpisodeNumber:00}_{request.Url}", connectionMultiplexer.GetDatabase());

            await using var handle = await @lock.TryAcquireAsync(TimeSpan.FromMinutes(30));
            if (handle == null)
            {
                logger.LogWarning("Download of {Url} is already in progress", request.Url);
                return;
            }

            await DownloadEpisode(request, logger, serviceProvider, messageBus);
        }
        else
        {
            await DownloadEpisode(request, logger, serviceProvider, messageBus);
        }
    }

    private static async ValueTask DownloadEpisode(DownloadRequest request, ILogger logger, IServiceProvider serviceProvider, IMessageBus messageBus)
    {
        await using var serviceScope = serviceProvider.CreateAsyncScope();

        logger.LogInformation("Starting download of {Url}", request.Url);

        var downloadOptions = serviceScope.ServiceProvider.GetRequiredService<IOptions<DownloadOptions>>();
        var daemonOptions = serviceScope.ServiceProvider.GetRequiredService<IOptions<DaemonOptions>>();
        var downloadServiceSolver = serviceScope.ServiceProvider.GetRequiredService<DownloadServiceSolver>();

        if (request.HevcOptions != null)
        {
            var ffmpegOptions = serviceScope.ServiceProvider.GetRequiredService<IOptions<FFmpegOptions>>();
            ffmpegOptions.Value.HevcProfile = request.HevcOptions.Profile;
            ffmpegOptions.Value.HevcQualityMin = request.HevcOptions.Qmin;
            ffmpegOptions.Value.HevcQualityMax = request.HevcOptions.Qmax;
        }

        var downloadService = downloadServiceSolver.GetService(request.Url);
        var episodesRange = new Ranges(request.EpisodeNumber, request.EpisodeNumber);
        var seasonsRange = new Ranges(request.SeasonNumber, request.SeasonNumber);

        var outputDirectoryOverride = string.IsNullOrEmpty(request.SeriesNameOverride) ? null : string.IsNullOrEmpty(downloadOptions.Value.DefaultOutputDirectory) ? null : Path.Combine(downloadOptions.Value.DefaultOutputDirectory, request.SeriesNameOverride);
        var episodes = await downloadService.DownloadEpisodes(request.Url.ToString(), 1,
            new DownloadEpisodeOptions(episodesRange, seasonsRange, outputDirectoryOverride));

        foreach (var downloadedEpisode in episodes)
            switch (downloadedEpisode.Status)
            {
                case DownloadedEpisodeStatus.Downloaded:
                    logger.LogInformation("Downloaded {Episode}", downloadedEpisode);

                    if (downloadedEpisode.FilePath != null && daemonOptions.Value.CheckVideoIntegrityAfterDownload)
                        await messageBus.PublishAsync(new CheckVideoIntegrityRequest(downloadedEpisode.FilePath, true));
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

        if (daemonOptions.Value.NotificationEnabled && serviceProvider.GetService<NotificationService>() is { } notificationService) await notificationService.SendNotifcationForDownloadedEpisodeAsync(episodes);
    }
}