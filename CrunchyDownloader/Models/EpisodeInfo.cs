namespace CrunchyDownloader.Models
{
    public class EpisodeInfo
    {
        public SeasonInfo SeasonInfo { get; init; }

        public string Name { get; init; }

        public string Url { get; init; }

        public int Number { get; init; }

        public string Id => $"S{SeasonInfo?.Season:00}E{Number:00}";
    }
}