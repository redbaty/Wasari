using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Wasari.Abstractions;
using Wasari.Ffmpeg;

namespace Wasari.Crunchyroll.Extensions
{
    public static class FfmpegServiceExtensions
    {
        public static Task Encode(this FfmpegService service, YoutubeDlEpisodeResult youtubeDlResult, string newVideoFile, DownloadParameters downloadParameters)
        {
            var subtitlesFiles = youtubeDlResult.Results
                .Where(i => i.Subtitles != null && !i.Source.Episode.SeasonInfo.Dubbed)
                .Take(1)
                .SelectMany(i => i.Subtitles)
                .Select(i => i.Path)
                .ToArray();
            
            var videoFiles = youtubeDlResult.Results
                .Where(i => i.Source != null && !string.IsNullOrEmpty(i.Source.LocalPath) && File.Exists(i.Source.LocalPath))
                .Select(i => i.Source)
                .ToArray();

            return service.Encode(youtubeDlResult.Episode?.Id, videoFiles,
                subtitlesFiles, newVideoFile, downloadParameters);
        }
    }
}