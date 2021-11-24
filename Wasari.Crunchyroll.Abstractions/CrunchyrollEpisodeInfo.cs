using Wasari.Abstractions;

namespace Wasari.Crunchyroll.Abstractions
{
    public class CrunchyrollEpisodeInfo : IEpisodeInfo
    {
        public string Id { get; init; }
        
        public string ThumbnailId { get; init; }
        
        public ISeasonInfo SeasonInfo { get; init; }

        public string Name { get; init; }

        public string Url { get; init; }

        public decimal SequenceNumber { get; init; }
        
        public string Number { get; init; }
        
        public bool Special { get; init; }

        public string FilePrefix => $"S{SeasonInfo?.Season:00}E{Number}";
    }
}