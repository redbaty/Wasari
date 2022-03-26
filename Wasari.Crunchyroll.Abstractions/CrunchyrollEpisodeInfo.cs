using System.Collections.Generic;
using Wasari.Abstractions;

namespace Wasari.Crunchyroll.Abstractions
{
    public class CrunchyrollEpisodeInfo : IEpisodeInfo
    {
        public string Id { get; init; }
        
        public string ThumbnailId { get; init; }
        
        public ISeasonInfo SeasonInfo { get; init; }
        
        public ISeriesInfo SeriesInfo { get; init; }

        public bool? Dubbed { get; init; }
        
        public string DubbedLanguage { get; init; }

        public string Name { get; init; }

        public string Url { get; init; }

        public decimal SequenceNumber { get; init; }
        
        public string Number { get; init; }
        
        public bool Special { get; init; }

        public ICollection<EpisodeInfoVideoSource> Sources { get; } = new List<EpisodeInfoVideoSource>();

        public bool Premium { get; init; }

        public string FilePrefix => $"S{SeasonInfo?.Season:00}E{Number}";
    }
}