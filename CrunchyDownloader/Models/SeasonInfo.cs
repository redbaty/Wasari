using System.Collections.Generic;

namespace CrunchyDownloader.Models
{
    public class SeasonInfo
    {
        public string Id { get; init; }
        
        public int Season { get; init; }
        
        public string Title { get; init; }
        
        public ICollection<EpisodeInfo> Episodes { get; init; }
    }
}