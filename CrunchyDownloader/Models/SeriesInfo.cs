using System.Collections.Generic;

namespace CrunchyDownloader.Models
{
    public class SeriesInfo
    {
        public string Id { get; init; }
        
        public string Name { get; init; }
        
        public ICollection<SeasonInfo> Seasons { get; init; }
    }
}