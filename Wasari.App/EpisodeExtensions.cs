using Wasari.Abstractions;

namespace Wasari.App;

internal static class EpisodeExtensions
{
    public static async IAsyncEnumerable<IEpisodeInfo> GetEpisodesGrouped(this IAsyncEnumerable<IEpisodeInfo> episodes)
    {
        var episodesGrouping = episodes
            .OrderBy(i => i.SeasonInfo.Season)
            .ThenBy(i => i.SequenceNumber)
            .GroupBy(i => new { i.Number, i.Name, i.SequenceNumber, i.Special, i.FilePrefix, i.SeriesInfo, i.SeasonInfo.Season });

        await foreach (var episodesGroup in episodesGrouping)
        {
            if (await episodesGroup.CountAsync() == 1)
                yield return await episodesGroup.SingleAsync();
            else
            {
                var dubbed = await episodesGroup.AnyAsync(o => o.Dubbed == true);
                var dubbedLanguages = await episodesGroup
                    .Where(o => !string.IsNullOrEmpty(o.DubbedLanguage))
                    .Select(i => i.DubbedLanguage)
                    .DefaultIfEmpty()
                    .AggregateAsync((x, y) => $"{x}, {y}");
                
                var season = new DummySeasonInfo
                {
                    Season = episodesGroup.Key.Season,
                    Dubbed = dubbed,
                    DubbedLanguage = dubbedLanguages ?? string.Empty,
                    Title = await episodesGroup.Where(i => !(i.Dubbed ?? false)).Select(i => i.SeasonInfo.Title).FirstOrDefaultAsync() ?? await episodesGroup.Select(i => i.SeasonInfo.Title).FirstAsync(),
                    Special = episodesGroup.Key.Special
                };

          
                yield return new DummyEpisodeInfo
                {
                    Name = episodesGroup.Key.Name,
                    Number = episodesGroup.Key.Number,
                    Special = episodesGroup.Key.Special,
                    FilePrefix = episodesGroup.Key.FilePrefix,
                    SequenceNumber = episodesGroup.Key.SequenceNumber,
                    Sources = await episodesGroup
                        .SelectMany(o => o.Sources.ToAsyncEnumerable())
                        .ToArrayAsync(),
                    SeriesInfo = episodesGroup.Key.SeriesInfo,
                    Dubbed = dubbed,
                    DubbedLanguage = dubbedLanguages,
                    SeasonInfo = season
                };
            }
        }
    }
}