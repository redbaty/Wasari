using Wasari.App.Abstractions;

namespace Wasari.App.Extensions;

public static class EpisodeExtensions
{
    private static bool FilterEpisodes(Ranges? range, int number)
    {
        if (range == null) return true;
        
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
}