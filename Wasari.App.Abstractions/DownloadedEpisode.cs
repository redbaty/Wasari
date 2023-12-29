namespace Wasari.App.Abstractions;

public record DownloadedEpisode(string? FilePath, DownloadedEpisodeStatus Status, IWasariEpisode Episode);