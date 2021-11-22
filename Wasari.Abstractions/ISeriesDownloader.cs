using System.Collections.Generic;

namespace Wasari.Abstractions
{
    public interface ISeriesDownloader<in T> where T : IEpisodeInfo
    {
        IAsyncEnumerable<DownloadedFile> DownloadEpisodes(IEnumerable<T> episodes, DownloadParameters downloadParameters);
    }
}