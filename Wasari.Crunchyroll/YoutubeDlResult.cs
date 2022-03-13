using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wasari.Abstractions;
using Wasari.Abstractions.Extensions;
using Wasari.Crunchyroll.Abstractions;

namespace Wasari.Crunchyroll
{
    public class YoutubeDlResult
    {
        public IEpisodeInfo Episode { get; init; }
        
        public EpisodeInfoVideoSource Source { get; init; }

        public List<DownloadedFile> Files { get; init; }

        public IEnumerable<DownloadedFile> Subtitles => Files?.Where(i => i.Type == FileType.Subtitle);

        public DownloadedFile TemporaryEpisodeFile => Files?.Single(i => i.Type == FileType.VideoFile);
    }
}