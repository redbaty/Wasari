using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wasari.Abstractions
{
    public interface ISeriesProvider
    {
        IAsyncEnumerable<IEpisodeInfo> GetEpisodes(string url);
    }
}