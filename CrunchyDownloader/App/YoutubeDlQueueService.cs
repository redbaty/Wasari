using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using CrunchyDownloader.Models;

namespace CrunchyDownloader.App
{
    internal class YoutubeDlQueueService
    {
        public YoutubeDlQueueService(YoutubeDlService youtubeDlService)
        {
            YoutubeDlService = youtubeDlService;
        }
        
        private Channel<YoutubeDlResult> ResultsChannel { get; } =
            Channel.CreateUnbounded<YoutubeDlResult>();

        public ChannelReader<YoutubeDlResult> Reader => ResultsChannel.Reader;

        private YoutubeDlService YoutubeDlService { get; }

        
        public async Task Start(IEnumerable<EpisodeInfo> episodes, DownloadParameters downloadParameters, int? poolSize)
        {
            var tasks = poolSize.HasValue
                ? new List<Task>(poolSize.Value)
                : new List<Task>();

            foreach (var episodeInfo in episodes)
            {
                if (poolSize.HasValue && tasks.Count >= poolSize)
                {
                    var task = await Task.WhenAny(tasks);
                    tasks.Remove(task);
                }

                var taskToQueue = YoutubeDlService.DownloadEpisode(episodeInfo, downloadParameters)
                    .ContinueWith(async t =>
                    {
                        if (t.IsCompletedSuccessfully)
                            await ResultsChannel.Writer.WriteAsync(await t);
                    });

                tasks.Add(taskToQueue);
            }

            await Task.WhenAll(tasks);
            ResultsChannel.Writer.Complete();
        }
    }
}