using System.Threading.Channels;
using Wasari.Abstractions;

namespace Wasari.YoutubeDl;

public class YoutubeDlQueue
{
    public YoutubeDlQueue(YoutubeDlService youtubeDlService, IEnumerable<IEpisodeInfo> episodes, int? poolSize, DownloadParameters downloadParameters, bool episodeGroupingEnabled)
    {
        YoutubeDlService = youtubeDlService;
        Episodes = episodes;
        PoolSize = poolSize;
        DownloadParameters = downloadParameters;

        if (episodeGroupingEnabled)
        {
            GroupedByEpisodeChannel = Channel.CreateUnbounded<YoutubeDlEpisodeResult>();
            GroupingTask = CreateGroupingTask();
        }
    }

    private int? PoolSize { get; }

    private Channel<YoutubeDlResult> ResultsChannel { get; } =
        Channel.CreateUnbounded<YoutubeDlResult>();
        
    public ChannelReader<YoutubeDlEpisodeResult>? ByEpisodeReader => GroupedByEpisodeChannel?.Reader;

    private Channel<YoutubeDlEpisodeResult>? GroupedByEpisodeChannel { get; }

    private IEnumerable<IEpisodeInfo> Episodes { get; }

    private YoutubeDlService YoutubeDlService { get; }

    private DownloadParameters DownloadParameters { get; }

    private Task? GroupingTask { get; }

    private async Task CreateGroupingTask()
    {
        var downloadedEpisodesDictionary = Episodes.ToDictionary(episodeInfo => episodeInfo.FilePrefix, episodeInfo => new
        {
            DownloadedEpisodes = new List<YoutubeDlResult>(),
            Total = episodeInfo.Sources.Count
        });

        await foreach (var youtubeDlResult in ResultsChannel.Reader.ReadAllAsync())
        {
            if(youtubeDlResult.Episode == null)
                continue;
            
            var resultsForEpisode = downloadedEpisodesDictionary[youtubeDlResult.Episode.FilePrefix];
            resultsForEpisode.DownloadedEpisodes.Add(youtubeDlResult);

            if (resultsForEpisode.DownloadedEpisodes.Count == resultsForEpisode.Total)
            {
                if (GroupedByEpisodeChannel != null)
                {
                    await GroupedByEpisodeChannel.Writer.WriteAsync(new YoutubeDlEpisodeResult
                    {
                        Episode = youtubeDlResult.Episode,
                        Results = resultsForEpisode.DownloadedEpisodes
                    });
                }

                downloadedEpisodesDictionary.Remove(youtubeDlResult.Episode.FilePrefix);
            }
        }

        if (downloadedEpisodesDictionary.Count > 0)
        {
            throw new InvalidOperationException("Episodes to download left");
        }
            
        GroupedByEpisodeChannel?.Writer.Complete();
    }

    public async Task Start()
    {
        var tasks = PoolSize.HasValue
            ? new List<Task>(PoolSize.Value)
            : new List<Task>();

        foreach (var episodeInfo in Episodes)
        {
            foreach (var episodeVideoSource in episodeInfo.Sources)
            {
                if (PoolSize.HasValue && tasks.Count >= PoolSize)
                {
                    var task = await Task.WhenAny(tasks);
                    tasks.Remove(task);
                }

                var taskToQueue = YoutubeDlService.DownloadEpisode(episodeVideoSource, DownloadParameters)
                    .ContinueWith(async t =>
                    {
                        if (t.IsCompletedSuccessfully)
                            await ResultsChannel.Writer.WriteAsync(await t);
                        else
                            ResultsChannel.Writer.Complete(t.Exception);
                    });

                tasks.Add(taskToQueue);
            }
        }

        await Task.WhenAll(tasks);
        ResultsChannel.Writer.Complete();

        if (GroupingTask != null)
        {
            await GroupingTask;
        }
    }
}