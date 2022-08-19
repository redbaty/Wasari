using Microsoft.Extensions.DependencyInjection;

namespace Wasari.YoutubeDlp;

public static class YoutubeDlpExtensions
{
    public static void AddYoutubeDlpServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<YoutubeDlpService>();
    }

    public static async IAsyncEnumerable<YoutubeDlEpisode> Group(this IAsyncEnumerable<YoutubeDlEpisode> episodes)
    {
        await foreach (var episodeGroup in episodes.GroupBy(i => new {i.Number, i.SeasonNumber}))
        {
            if (await episodeGroup.CountAsync() == 1)
            {
                yield return await episodeGroup.SingleAsync();
                continue;
            }
             
            var videoInput = await episodeGroup.SingleAsync(i => i.Language == "ja-JP");
            var inputs = await episodeGroup.GroupBy(i => i.Language)
                .SelectAwait(i => i.FirstAsync())
                .SelectMany(i => i.RequestedDownloads.ToAsyncEnumerable())
                .Select(i => i with { Vcodec = i.Language == "ja-JP" ? i.Vcodec : null })
                .ToArrayAsync();
            var subs = await episodeGroup.SelectMany(i => i.Subtitles.ToAsyncEnumerable())
                .GroupBy(i => i.Key)
                .SelectAwait(i => i.FirstAsync())
                .ToDictionaryAsync(i => i.Key, i => i.Value);

            yield return videoInput with { Number = episodeGroup.Key.Number, RequestedDownloads = inputs, Subtitles = subs, WasGrouped = true};
        }
    }
}