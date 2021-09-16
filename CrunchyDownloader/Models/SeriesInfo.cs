using System.Collections.Generic;

namespace CrunchyDownloader.Models
{
    public class SeriesInfo
    {
        public string Name { get; }
        
        public ICollection<SeasonInfo> Seasons { get; }

        public SeriesInfo(string name, ICollection<SeasonInfo> seasons)
        {
            Name = name;
            Seasons = seasons;
        }
    }
}