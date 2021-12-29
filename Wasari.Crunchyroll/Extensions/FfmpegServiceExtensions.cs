using System.Linq;
using System.Threading.Tasks;
using Wasari.Abstractions;
using Wasari.Ffmpeg;

namespace Wasari.Crunchyroll.Extensions
{
    internal static class FfmpegServiceExtensions
    {
        public static Task Encode(this FfmpegService service, YoutubeDlResult youtubeDlResult, string newVideoFile, DownloadParameters downloadParameters)
        {
            return service.Encode(youtubeDlResult.Episode?.FilePrefix, youtubeDlResult.TemporaryEpisodeFile?.Path,
                youtubeDlResult.Subtitles?.Select(i => i.Path).ToArray(), newVideoFile, downloadParameters);
        }
    }
}