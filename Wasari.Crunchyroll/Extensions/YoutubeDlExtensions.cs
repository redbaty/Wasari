using System;
using System.Collections.Generic;
using System.Linq;
using Wasari.Abstractions;
using Wasari.Ffmpeg;
using Wasari.YoutubeDl;

namespace Wasari.Crunchyroll.Extensions;

public static class YoutubeDlExtensions
{
    public static FfmpegEpisodeToEncode ToFfmpeg(this YoutubeDlEpisodeResult youtubeDlEpisodeResult, IEnumerable<DownloadedFile> additionalSubs = null) =>
        new()
        {
            Episode = youtubeDlEpisodeResult.Episode,
            Sources = youtubeDlEpisodeResult.Results?.Select(o => o.Source).ToArray(),
            Subtitles = youtubeDlEpisodeResult.Results?.SelectMany(o => o.Subtitles ?? Array.Empty<DownloadedFile>()).Concat(additionalSubs?.Where(i => i.Type == FileType.Subtitle) ?? Array.Empty<DownloadedFile>()).ToArray()
        };
}