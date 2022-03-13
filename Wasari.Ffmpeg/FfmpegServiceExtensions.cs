using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Wasari.Abstractions;

namespace Wasari.Ffmpeg
{
    public static class FfmpegServiceExtensions
    {
        public static Task Encode(this FfmpegService service, FfmpegEpisodeToEncode episodeToEncode, string newVideoFile, DownloadParameters downloadParameters)
        {
            var subtitlesFiles = episodeToEncode.Subtitles?
                .Where(i => File.Exists(i.Path))
                .Select(i => i.Path)
                .ToArray();
            
            var videoFiles = episodeToEncode.Sources
                .Where(i => i != null && !string.IsNullOrEmpty(i.LocalPath) && File.Exists(i.LocalPath))
                .ToArray();

            return service.Encode(episodeToEncode.Episode.Id, videoFiles,
                subtitlesFiles, newVideoFile, downloadParameters);
        }
    }
}