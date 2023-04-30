using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wasari.App;
using Wasari.Tvdb.Abstractions;
using Wasari.Tvdb.Api.Client;

namespace Wasari.Crunchyroll;

internal static partial class EpisodeExtensions
{
    private static string NormalizeUsingRegex(this string str) => string.Join(string.Empty, EpisodeTitleNormalizeRegex().Matches(str).Select(o => o.Value));

    public static async IAsyncEnumerable<ApiEpisode> EnrichWithWasariApi(this IAsyncEnumerable<ApiEpisode> episodes, IServiceProvider serviceProvider, IOptions<DownloadOptions> downloadOptions)
    {
        var wasariTvdbApi = downloadOptions.Value.TryEnrichEpisodes ? serviceProvider.GetService<IWasariTvdbApi>() : null;

        if (wasariTvdbApi != null)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<IWasariTvdbApi>>();
            logger.LogInformation("Trying to enrich episodes with Wasari.Tvdb");

            var episodesArray = await episodes.ToArrayAsync();

            var moreThanOneEpisodeWithSameTitle = episodesArray
                .GroupBy(i => i.Title)
                .Any(i => i.Count() > 1);

            if (moreThanOneEpisodeWithSameTitle)
            {
                logger.LogWarning("Disabling Wasari.Tvdb enrichment because more than one episode has the same title");
            }
            else
            {
                var seriesName = episodesArray.Select(o => o.SeriesTitle)
                    .Distinct()
                    .ToArray();

                if (seriesName.Length == 1)
                {
                    var wasariApiEpisodes = await wasariTvdbApi.GetEpisodesAsync(seriesName.Single())
                        .ContinueWith(t =>
                        {
                            if (t.IsCompletedSuccessfully)
                            {
                                return t.Result;
                            }

                            logger.LogError(t.Exception, "Error while getting episodes from Wasari.Tvdb");
                            return null;
                        });

                    if (wasariApiEpisodes != null)
                    {
                        var episodesLookup = wasariApiEpisodes
                            .Where(i => !i.IsMovie)
                            .Distinct()
                            .ToLookup(i => i.Name);

                        foreach (var episode in episodesArray)
                        {
                            var wasariEpisode = episodesLookup[episode.Title].SingleOrDefault() ?? FindEpisodeByNormalizedName(wasariApiEpisodes, episode) ?? FindEpisodeByNormalizedWordMatch(wasariApiEpisodes, episode);

                            if (wasariEpisode == null)
                            {
                                logger.LogWarning("Failed to match episode {EpisodeTitle} with Wasari.Tvdb", episode.Title);
                                yield return episode;
                            }

                            if (wasariEpisode != null)
                                yield return episode with
                                {
                                    SeasonNumber = wasariEpisode.SeasonNumber ?? episode.SeasonNumber,
                                    EpisodeNumber = wasariEpisode.Number ?? episode.EpisodeNumber
                                };
                        }

                        yield break;
                    }
                }
            }

            foreach (var episode in episodesArray)
            {
                yield return episode;
            }

            yield break;
        }


        await foreach (var episode in episodes)
        {
            yield return episode;
        }
    }

    private static Episode FindEpisodeByNormalizedName(IEnumerable<Episode> wasariApiEpisodes, ApiEpisode episode)
    {
        var episodeName = episode.Title.NormalizeUsingRegex();

        return wasariApiEpisodes
            .Where(i => !i.IsMovie)
            .SingleOrDefault(o => o.Name.NormalizeUsingRegex() == episodeName);
    }

    private static Episode FindEpisodeByNormalizedWordMatch(IEnumerable<Episode> wasariApiEpisodes, ApiEpisode episode)
    {
        var episodeName = episode.Title.NormalizeUsingRegex();
        var split = episodeName.Split(' ')
            .Select(i => i.ToLowerInvariant())
            .ToHashSet();

        return wasariApiEpisodes
            .Where(i => !i.IsMovie)
            .SingleOrDefault(o =>
            {
                var normalizeUsingRegex = o.Name.NormalizeUsingRegex();
                var x = normalizeUsingRegex.Split(' ')
                    .Select(i => i.ToLowerInvariant())
                    .ToHashSet();

                return x.Count == split.Count && x.All(split.Contains);
            });
    }

    [GeneratedRegex("[a-zA-Z0-9 ]+")]
    private static partial Regex EpisodeTitleNormalizeRegex();
}