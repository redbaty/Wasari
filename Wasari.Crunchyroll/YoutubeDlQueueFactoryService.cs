using System.Collections.Generic;
using Wasari.Abstractions;

namespace Wasari.Crunchyroll
{
    public class YoutubeDlQueueFactoryService
    {
        public YoutubeDlQueueFactoryService(YoutubeDlService youtubeDlService)
        {
            YoutubeDlService = youtubeDlService;
        }
        
        private YoutubeDlService YoutubeDlService { get; }

        public YoutubeDlQueue CreateQueue(IEnumerable<IEpisodeInfo> episodes, DownloadParameters downloadParameters, int? poolSize, bool enableGroupByEpisode = false)
        {
            return new YoutubeDlQueue(YoutubeDlService, episodes, poolSize, downloadParameters, enableGroupByEpisode);
        }
    }
}