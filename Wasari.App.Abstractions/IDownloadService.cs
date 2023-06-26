namespace Wasari.App.Abstractions;

public interface IDownloadService
{
    Task<DownloadedEpisode[]> DownloadEpisodes(string url, int levelOfParallelism, DownloadEpisodeOptions options);
}