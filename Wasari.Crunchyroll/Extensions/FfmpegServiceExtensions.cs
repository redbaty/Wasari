using System.Linq;
using System.Threading.Tasks;
using Wasari.Abstractions;
using Wasari.Ffmpeg;

namespace Crunchyroll.API.Extensions
{
    internal static class FfmpegServiceExtensions
    {
        public static Task Encode(this FfmpegService service, YoutubeDlResult youtubeDlResult, string newVideoFile, DownloadParameters downloadParameters)
        {
            return service.Encode(youtubeDlResult.Episode?.FilePrefix, youtubeDlResult.TemporaryEpisodeFile?.Path,
                youtubeDlResult.Subtitles?.Select(i => i.Path), newVideoFile, downloadParameters);
        }
    }
}