namespace CrunchyDownloader.Models
{
    public class EpisodeInfo
    {
        public string Id { get; init; }
        
        public string ThumbnailId { get; init; }
        
        public SeasonInfo SeasonInfo { get; init; }

        public string Name { get; init; }

        public string Url { get; init; }

        public decimal SequenceNumber { get; init; }
        
        public string Number { get; init; }

        public string FilePrefix => $"S{SeasonInfo?.Season:00}E{Number}";
    }
}