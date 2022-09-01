using Microsoft.Extensions.DependencyInjection;

namespace Wasari.YoutubeDlp;

public static class YoutubeDlpExtensions
{
    public static void AddYoutubeDlpServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<YoutubeDlpService>();
    }

    public static async IAsyncEnumerable<YoutubeDlEpisode> FillSeasonAbsoluteNumbers(this IAsyncEnumerable<YoutubeDlEpisode> episodes)
    {
        var currentSeason = 1;
        await foreach (var episodeGroup in episodes.OrderBy(i => i.SeasonNumber).GroupBy(i => new { i.SeasonNumber, i.SeasonId }))
        {
            await foreach (var youtubeDlEpisode in episodeGroup)
            {
                yield return youtubeDlEpisode with { AbsoluteSeasonNumber = currentSeason };
            }

            currentSeason++;
        }
    }

    public static async IAsyncEnumerable<YoutubeDlEpisode> FillEpisodesAbsoluteNumbers(this IAsyncEnumerable<YoutubeDlEpisode> episodes)
    {
        var currentEpisode = 1;
        await foreach (var youtubeDlEpisode in episodes.OrderBy(i => i.AbsoluteSeasonNumber ?? i.SeasonNumber).ThenBy(i => i.Number))
        {
            if (youtubeDlEpisode.Number <= 0)
            {
                yield return youtubeDlEpisode;
                continue;
            }

            yield return youtubeDlEpisode with
            {
                AbsoluteNumber = currentEpisode
            };

            currentEpisode++;
        }
    }

    public static async IAsyncEnumerable<YoutubeDlEpisode> FixEpisodesNumbers(this IAsyncEnumerable<YoutubeDlEpisode> episodes)
    {
        var currentEpisode = 1;
        var currentSeason = 1;
        var wasSpecial = false;
        string? seasonId = null;

        await foreach (var youtubeDlEpisode in episodes.OrderBy(i => i.AbsoluteNumber.HasValue ? (i.AbsoluteSeasonNumber ?? i.SeasonNumber) : 0).ThenBy(i => i.AbsoluteNumber ?? i.Number))
        {
            if (seasonId != null && seasonId != youtubeDlEpisode.SeasonId)
            {
                currentEpisode = 1;

                if (!wasSpecial)
                    currentSeason++;
            }

            if (!youtubeDlEpisode.AbsoluteNumber.HasValue)
            {
                yield return youtubeDlEpisode with { SeasonNumber = 0, Number = currentEpisode };
                wasSpecial = true;
            }
            else
            {
                yield return youtubeDlEpisode with
                {
                    Number = currentEpisode,
                    SeasonNumber = currentSeason
                };

                wasSpecial = false;
            }

            currentEpisode++;
            seasonId = youtubeDlEpisode.SeasonId;
        }
    }

    public static async IAsyncEnumerable<YoutubeDlEpisode> Group(this IAsyncEnumerable<YoutubeDlEpisode> episodes)
    {
        await foreach (var episodeGroup in episodes.GroupBy(i => new { i.Number, i.AbsoluteSeasonNumber }))
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

            yield return videoInput with { Number = episodeGroup.Key.Number, RequestedDownloads = inputs, Subtitles = subs, WasGrouped = true };
        }
    }
}