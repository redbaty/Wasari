using Wasari.App;
using Wasari.App.Abstractions;
using Wasari.Daemon.Models;

namespace Wasari.Daemon.Handlers;

public class DownloadRequestHandler
{
    public async ValueTask Handle(DownloadRequest request, ILogger<DownloadRequestHandler> logger, DownloadServiceSolver downloadServiceSolver)
    {
        logger.LogInformation("Starting download of {Url}", request.Url);
        
        var downloadService = downloadServiceSolver.GetService(request.Url);
        var episodesRange = new Ranges(request.EpisodeNumber, request.EpisodeNumber);
        var seasonsRange = new Ranges(request.SeasonNumber, request.SeasonNumber);
        var episodes = await downloadService.DownloadEpisodes(request.Url.ToString(), 1, new DownloadEpisodeOptions(episodesRange, seasonsRange, request.OutputDirectoryOverride));
        
        foreach (var downloadedEpisode in episodes.Where(i => i.Success))
        {
            logger.LogInformation("Downloaded {Episode}", downloadedEpisode);
        }
        
        foreach (var downloadedEpisode in episodes.Where(i => !i.Success))
        {
            logger.LogError("Failed to download {Episode}", downloadedEpisode);
        }
    }
}