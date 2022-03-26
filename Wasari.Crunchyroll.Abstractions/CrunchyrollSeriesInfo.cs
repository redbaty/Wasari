using System.Collections.Generic;
using Wasari.Abstractions;

namespace Wasari.Crunchyroll.Abstractions
{
    public class CrunchyrollSeriesInfo : ISeriesInfo
    {
        public string Id { get; init; }

        public string Name { get; init; }

        public ICollection<ISeasonInfo> Seasons { get; init; }
    }
}