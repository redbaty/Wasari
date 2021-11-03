using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrunchyDownloader.Extensions;
using CrunchyDownloader.Models;

namespace CrunchyDownloader.App
{
    internal class YoutubeDlResult
    {
        public EpisodeInfo Episode { get; init; }

        public List<DownloadedFile> Files { get; init; }

        public IEnumerable<DownloadedFile> Subtitles => Files?.Where(i => i.Type == FileType.Subtitle);

        public DownloadedFile TemporaryEpisodeFile => Files?.Single(i => i.Type == FileType.VideoFile);

        public string FinalEpisodeFile(DownloadParameters downloadParameters) => Path.Combine(
            downloadParameters.OutputDirectory,
            $"{Episode.FilePrefix} - {Episode.Name.AsSafePath()}.mkv");
    }
}