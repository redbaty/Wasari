namespace Wasari.App.Abstractions;

public record DownloadedEpisode(string? FilePath, bool Success, IWasariEpisode Episode);