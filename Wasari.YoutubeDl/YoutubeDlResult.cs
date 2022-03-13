using Wasari.Abstractions;

namespace Wasari.YoutubeDl
{
    public class YoutubeDlResult
    {
        public IEpisodeInfo? Episode { get; init; }
        
        public EpisodeInfoVideoSource? Source { get; init; }

        public List<DownloadedFile>? Files { get; init; }

        public IEnumerable<DownloadedFile>? Subtitles => Files?.Where(i => i.Type == FileType.Subtitle);

        public DownloadedFile? TemporaryEpisodeFile => Files?.Single(i => i.Type == FileType.VideoFile);
    }
}