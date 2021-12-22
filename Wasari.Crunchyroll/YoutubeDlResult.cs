using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wasari.Abstractions;
using Wasari.Abstractions.Extensions;
using Wasari.Crunchyroll.Abstractions;

namespace Wasari.Crunchyroll
{
    internal class YoutubeDlResult
    {
        public CrunchyrollEpisodeInfo Episode { get; init; }

        public List<DownloadedFile> Files { get; init; }

        public IEnumerable<DownloadedFile> Subtitles => Files?.Where(i => i.Type == FileType.Subtitle);

        public DownloadedFile TemporaryEpisodeFile => Files?.Single(i => i.Type == FileType.VideoFile);

        private string FinalEpisodeFileExtension(DownloadParameters downloadParameters)
        {
            if (downloadParameters.UseHevc || downloadParameters.Subtitles)
            {
                return ".mkv";
            }

            return ".mp4";
        }
        
        public string FinalEpisodeFile(DownloadParameters downloadParameters)
        {
            var finalEpisodeFileName = $"{Episode.FilePrefix} - {Episode.Name.AsSafePath()}{FinalEpisodeFileExtension(downloadParameters)}";
            
            return downloadParameters.CreateSeasonFolder
                ? Path.Combine(
                    downloadParameters.OutputDirectory,
                    $"Season {Episode.SeasonInfo.Season}",
                    finalEpisodeFileName)
                : Path.Combine(
                    downloadParameters.OutputDirectory,
                    finalEpisodeFileName);
        }
    }
}