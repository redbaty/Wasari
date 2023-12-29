using Wasari.FFmpeg;

namespace Wasari.Daemon.Models;

public record DownloadRequestHevcOptions(HevcProfile Profile, int? Qmin, int? Qmax);

public record DownloadRequest(Uri Url, int EpisodeNumber, int SeasonNumber, string? SeriesNameOverride, DownloadRequestHevcOptions? HevcOptions);