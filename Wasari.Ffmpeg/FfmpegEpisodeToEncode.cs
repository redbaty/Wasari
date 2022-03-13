using System.Collections.Generic;
using Wasari.Abstractions;

namespace Wasari.Ffmpeg;

public class FfmpegEpisodeToEncode
{
    public IEpisodeInfo Episode { get; init; }
        
    public ICollection<EpisodeInfoVideoSource> Sources { get; init; }
        
    public ICollection<DownloadedFile> Subtitles { get; init; }
}