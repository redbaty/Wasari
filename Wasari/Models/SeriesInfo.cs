using System.Collections.Generic;

namespace Wasari.Models
{
    public class SeriesInfo
    {
        public string Id { get; init; }
        
        public string Name { get; init; }
        
        public ICollection<SeasonInfo> Seasons { get; init; }
    }
}