namespace Wasari.App.Abstractions;

public enum DownloadedEpisodeStatus
{
    Downloaded,
    AlreadyExists,
    Failed
}

public record DownloadedEpisode(string? FilePath, DownloadedEpisodeStatus Status, IWasariEpisode Episode);