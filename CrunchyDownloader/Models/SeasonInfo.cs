namespace CrunchyDownloader.Models
{
    public class SeasonInfo
    {
        public int Season { get; }
        
        public string Title { get; }

        public SeasonInfo(int season, string title)
        {
            Season = season;
            Title = title;
        }
    }
}