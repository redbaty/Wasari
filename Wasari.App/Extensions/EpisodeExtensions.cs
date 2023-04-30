using Wasari.App.Abstractions;

namespace Wasari.App.Extensions;

public static class EpisodeExtensions
{
    private static bool FilterEpisodes(Ranges? range, int? number)
    {
        if (range == null || number == null) return true;
        
        var flag = true;
        
        if (flag && range.Minimum.HasValue)
        {
            flag = number >= range.Minimum;
        }

        if (flag && range.Maximum.HasValue)
        {
            flag = number <= range.Maximum;
        }

        return flag;
    }

    public static IAsyncEnumerable<WasariEpisode> FilterEpisodes(this IAsyncEnumerable<WasariEpisode> episodes, Ranges? episodesRange, Ranges? seasonsRange) =>
        episodes.Where(episode => FilterEpisodes(episodesRange, episode.Number) && FilterEpisodes(seasonsRange, episode.SeasonNumber));

    public static async IAsyncEnumerable<WasariEpisode> EnsureUniqueEpisodes(this IAsyncEnumerable<WasariEpisode> episodes)
    {
        await foreach (var episodeGroup in episodes.GroupBy(i => new {i.SeasonNumber, i.Number}))
        {
            if (episodeGroup.Key.SeasonNumber > 0)
            {
                var c = await episodeGroup.CountAsync();
                
                if(c > 1)
                    throw new InvalidOperationException($"Multiple episodes with the same number {episodeGroup.Key.Number} in season {episodeGroup.Key.SeasonNumber}");
            }
            
            await foreach (var episode in episodeGroup)
                yield return episode;
        }
    }
}