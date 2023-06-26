namespace Wasari.Daemon.Models;

public record DownloadRequest(Uri Url, int EpisodeNumber, int SeasonNumber, string? OutputDirectoryOverride);