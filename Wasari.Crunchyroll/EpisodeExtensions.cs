using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using F23.StringSimilarity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wasari.App;
using Wasari.Tvdb.Abstractions;
using Wasari.Tvdb.Api.Client;

namespace Wasari.Crunchyroll;

public static partial class EpisodeExtensions
{
    private static readonly NormalizedLevenshtein Levenshtein = new();

    [GeneratedRegex("[a-zA-Z0-9 ]+")]
    private static partial Regex EpisodeTitleNormalizeRegex();

    private static string NormalizeUsingRegex(this string str)
    {
        return string.Join(string.Empty, EpisodeTitleNormalizeRegex().Matches(str).Select(o => o.Value));
    }

    public static IAsyncEnumerable<ApiEpisode> EnrichWithWasariApi(this IAsyncEnumerable<ApiEpisode> episodes, IServiceProvider serviceProvider, IOptions<DownloadOptions> downloadOptions)
    {
        if (downloadOptions.Value.TryEnrichEpisodes)
            return EnrichWithWasariApi(episodes, serviceProvider)
                .Where(i => !downloadOptions.Value.OnlyDownloadEnrichedEpisodes || i.WasEnriched);

        return episodes;
    }

    public static async IAsyncEnumerable<ApiEpisode> EnrichWithWasariApi(this IAsyncEnumerable<ApiEpisode> episodes, IServiceProvider serviceProvider)
    {
        var wasariTvdbApi = serviceProvider.GetService<IWasariTvdbApi>();

        if (wasariTvdbApi != null)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<IWasariTvdbApi>>();
            logger.LogInformation("Trying to enrich episodes with Wasari.Tvdb");

            var episodesArray = await episodes.ToArrayAsync();

            var episodesWithMoreThanOneEpisodeWithSameTitle = episodesArray
                .GroupBy(i => new { i.Title, i.AudioLocale })
                .Where(i => i.Count() > 1)
                .SelectMany(i => i)
                .ToArray();

            foreach (var gEpisodes in episodesArray
                         .Except(episodesWithMoreThanOneEpisodeWithSameTitle)
                         .GroupBy(i => new { i.SeriesTitle, i.AudioLocale }))
            {
                var wasariApiEpisodes = await wasariTvdbApi
                    .GetEpisodesAsync(gEpisodes.Key.SeriesTitle)
                    .ContinueWith(t =>
                    {
                        if (t.IsCompletedSuccessfully) return t.Result;

                        logger.LogError(t.Exception, "Error while getting episodes from Wasari.Tvdb");
                        return null;
                    });

                if (wasariApiEpisodes != null)
                {
                    var episodesLookup = wasariApiEpisodes
                        .Where(i => !i.IsMovie)
                        .Distinct()
                        .ToLookup(i => i.Name);

                    var unmatchedEpisodes = new List<ApiEpisode>();

                    foreach (var episode in gEpisodes)
                    {
                        var wasariEpisode = episodesLookup[episode.Title]
                                                .Where(i => !i.Matched)
                                                .SingleOrDefaultIfMultiple()
                                            ?? episodesLookup[episode.Title.Trim()]
                                                .Where(i => !i.Matched)
                                                .SingleOrDefaultIfMultiple()
                                            ?? FindEpisodeByNormalizedName(wasariApiEpisodes, episode)
                                            ?? FindEpisodeByNormalizedWordMatch(wasariApiEpisodes, episode);

                        if (wasariEpisode == null)
                            unmatchedEpisodes.Add(episode);
                        else
                            yield return CreateMatchedEpisode(ref wasariEpisode, episode);
                    }

                    foreach (var unmatchedEpisode in unmatchedEpisodes)
                    {
                        var wasariEpisode = FindEpisodeByNormalizedWordProximity(wasariApiEpisodes, unmatchedEpisode);

                        if (wasariEpisode != null)
                        {
                            yield return CreateMatchedEpisode(ref wasariEpisode, unmatchedEpisode);
                        }
                        else
                        {
                            logger.LogWarning("Failed to match episode {Episode} with Wasari.Tvdb", unmatchedEpisode);
                            yield return unmatchedEpisode;
                        }
                    }
                }
                else
                {
                    foreach (var apiEpisode in gEpisodes) yield return apiEpisode;
                }
            }

            foreach (var apiEpisode in episodesWithMoreThanOneEpisodeWithSameTitle) yield return apiEpisode;
        }
        else
        {
            await foreach (var episode in episodes) yield return episode;
        }
    }

    private static ApiEpisode CreateMatchedEpisode(ref WasariTvdbEpisode wasariEpisode, ApiEpisode unmatchedEpisode)
    {
        wasariEpisode.Matched = true;

        return unmatchedEpisode with
        {
            SeasonNumber = wasariEpisode.SeasonNumber ?? unmatchedEpisode.SeasonNumber,
            EpisodeNumber = wasariEpisode.Number ?? unmatchedEpisode.EpisodeNumber,
            WasEnriched = true
        };
    }

    private static WasariTvdbEpisode FindEpisodeByNormalizedName(IEnumerable<WasariTvdbEpisode> wasariApiEpisodes, ApiEpisode episode)
    {
        var episodeName = episode.Title.NormalizeUsingRegex();

        return wasariApiEpisodes
            .Where(i => !i.IsMovie && !i.Matched)
            .SingleOrDefault(o => o.Name.NormalizeUsingRegex() == episodeName);
    }
    
    private static WasariTvdbEpisode FindEpisodeByNormalizedWordProximity(IEnumerable<WasariTvdbEpisode> wasariApiEpisodes, ApiEpisode episode)
    {
        var episodeName = episode.Title
            .ToLowerInvariant()
            .NormalizeUsingRegex();

        var wasariTvdbEpisodes = wasariApiEpisodes.ToList();
        var result = FindEpisodeByNormalizedWordProximity(wasariTvdbEpisodes, episode, episodeName);

        if (result == null && episodeName.Contains(episode.SeasonTitle, StringComparison.InvariantCultureIgnoreCase))
        {
            result = FindEpisodeByNormalizedWordProximity(wasariTvdbEpisodes, episode, episodeName.Replace(episode.SeasonTitle, string.Empty, StringComparison.InvariantCultureIgnoreCase));
        }

        return result;
    }
    
    private static WasariTvdbEpisode FindEpisodeByNormalizedWordProximity(IEnumerable<WasariTvdbEpisode> wasariApiEpisodes, ApiEpisode episode, string episodeName)
    {
        var possibleEpisodes = wasariApiEpisodes
            .Where(o => !o.Matched)
            .Select(i => new
            {
                Episode = i,
                Distance = Levenshtein.Distance(i.Name
                    .ToLowerInvariant()
                    .NormalizeUsingRegex(), episodeName)
            })
            .OrderBy(i => i.Distance)
            .Take(2)
            .ToList();

        if (possibleEpisodes.Count <= 1)
            return possibleEpisodes
                .Select(i => i.Episode)
                .SingleOrDefault();

        var delta = possibleEpisodes[1].Distance - possibleEpisodes[0].Distance;
        if (possibleEpisodes[0].Distance < 0.5 && delta > 0.4) return possibleEpisodes[0].Episode;

        if (possibleEpisodes
                .Where(i => i.Episode.SeasonNumber == episode.SeasonNumber && (i.Episode.Number == episode.EpisodeNumber || i.Episode.CalculatedAbsoluteNumber == episode.SequenceNumber))
                .SingleOrDefaultIfMultiple() is { } ep)
            return ep.Episode;
        
        return default;
    }

    private static WasariTvdbEpisode FindEpisodeByNormalizedWordMatch(IEnumerable<WasariTvdbEpisode> wasariApiEpisodes, ApiEpisode episode)
    {
        var episodeName = episode.Title.NormalizeUsingRegex();
        var split = episodeName
            .ToLowerInvariant()
            .Split(' ')
            .ToHashSet();

        return wasariApiEpisodes
            .Where(i => !i.IsMovie && !i.Matched)
            .SingleOrDefault(o =>
            {
                var normalizeUsingRegex = o.Name.NormalizeUsingRegex();
                var x = normalizeUsingRegex.Split(' ')
                    .Select(i => i.ToLowerInvariant())
                    .ToHashSet();

                return x.Count == split.Count && x.All(split.Contains);
            });
    }
}