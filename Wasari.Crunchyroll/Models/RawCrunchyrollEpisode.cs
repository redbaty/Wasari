namespace Wasari.Crunchyroll.Models
{
    internal class RawCrunchyrollEpisode
    {
        public string Id { get; init; }

        public string ThumbnailId { get; init; }

        public string SeasonId { get; init; }

        public decimal? Number { get; init; }

        public int SeasonNumber { get; init; }

        public string Name { get; init; }

        public string Url { get; init; }

        public bool Special { get; init; }
        
        public bool Premium { get; init; }
    }
}