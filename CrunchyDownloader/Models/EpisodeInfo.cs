namespace CrunchyDownloader.Models
{
    public class EpisodeInfo
    {
        public SeriesInfo Series { get; }
        
        public SeasonInfo SeasonInfo { get; }
        
        public string Name { get; }
        
        public string Url { get; }
        
        public int Number { get; }

        public EpisodeInfo(SeriesInfo series, string name, string url, SeasonInfo seasonInfo, int number)
        {
            Series = series;
            Name = name;
            Url = url;
            SeasonInfo = seasonInfo;
            Number = number;
        }
    }
}