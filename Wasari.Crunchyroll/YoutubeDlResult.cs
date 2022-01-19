using System;
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
            var fileName = string.Format(downloadParameters.FileMask ?? "{0} - {1}", Episode.FilePrefix, Episode.Name.AsSafePath());
            var finalEpisodeFileName = $"{fileName}{FinalEpisodeFileExtension(downloadParameters)}";
            var outputDirectory = downloadParameters.OutputDirectory ?? Environment.CurrentDirectory;
            
            return downloadParameters.CreateSeasonFolder
                ? Path.Combine(
                    outputDirectory,
                    $"Season {Episode.SeasonInfo.Season}",
                    finalEpisodeFileName)
                : Path.Combine(
                    outputDirectory,
                    finalEpisodeFileName);
        }
    }
}