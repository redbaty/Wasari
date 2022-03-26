using System.Collections.Generic;

namespace Wasari.Abstractions
{
    public interface ISeriesProvider
    {
        IAsyncEnumerable<IEpisodeInfo> GetEpisodes(string url);
    }
}