namespace Wasari.App.Abstractions;

public record DownloadEpisodeOptions(Ranges? EpisodesRange, Ranges? SeasonsRange, string? OutputDirectoryOverride);