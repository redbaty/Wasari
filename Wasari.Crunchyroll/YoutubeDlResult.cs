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

        public string FinalEpisodeFile(DownloadParameters downloadParameters) => downloadParameters.CreateSeasonFolder ? Path.Combine(
            downloadParameters.OutputDirectory,
            $"Season {Episode.SeasonInfo.Season}",
            $"{Episode.FilePrefix} - {Episode.Name.AsSafePath()}.mkv") : Path.Combine(
            downloadParameters.OutputDirectory,
            $"{Episode.FilePrefix} - {Episode.Name.AsSafePath()}.mkv");
    }
}