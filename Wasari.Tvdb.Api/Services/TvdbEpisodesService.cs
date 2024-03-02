using Wasari.Tvdb.Abstractions;
using Wasari.Tvdb.Models;

namespace Wasari.Tvdb.Api.Services;

public class TvdbEpisodesService
{
    public TvdbEpisodesService(ITvdbApi tvdbApi)
    {
        TvdbApi = tvdbApi;
    }

    private ITvdbApi TvdbApi { get; }

    public async ValueTask<IResult> GetEpisodes(string query)
    {
        var searchResult = await TvdbApi.SearchAsync(query);
        
        if(searchResult is null)
            return Results.BadRequest(new TvdbApiErrorResponse(StatusCodes.Status400BadRequest, "Invalid query", "No series found"));
        
        var tvdbSearchResponseSeries = searchResult.Data;

        var series = tvdbSearchResponseSeries?.SingleOrDefaultIfMultiple();

        if (tvdbSearchResponseSeries is { Count: > 1 })
        {
            series ??= tvdbSearchResponseSeries
                .Where(i => string.Equals(i.Name, query, StringComparison.InvariantCultureIgnoreCase))
                .SingleOrDefaultIfMultiple();

            series ??= tvdbSearchResponseSeries
                .Where(i => i.Aliases != null && i.Aliases.Any(x => string.Equals(x, query, StringComparison.InvariantCultureIgnoreCase)))
                .SingleOrDefaultIfMultiple();

            series ??= tvdbSearchResponseSeries
                .Where(i => i.Translations != null && i.Translations.Any(x => string.Equals(x.Value, query, StringComparison.InvariantCultureIgnoreCase)))
                .SingleOrDefaultIfMultiple();
        }

        if (series == null)
            return Results.BadRequest(new TvdbApiErrorResponse(StatusCodes.Status400BadRequest, "Invalid query", tvdbSearchResponseSeries is { Count: > 0 } ? "Multiple series found" : "No series found"));

        var seriesWithEpisodes = await TvdbApi.GetSeriesAsync(series.TvdbId);

        var currentEpiosdeNumber = 1;

        return Results.Ok(seriesWithEpisodes?.Data.Episodes
            .Where(i => !string.IsNullOrEmpty(i.Name))
            .OrderBy(i => i.SeasonNumber)
            .ThenBy(i => i.Number)
            .Select(ep =>
            {
                var episode = new WasariTvdbEpisode(ep.Id, ep.Name, ep.SeasonNumber, ep.Number, ep.IsMovie switch
                    {
                        0 => false,
                        1 => true,
                        _ => throw new ArgumentException("IsMovie flag is not 0 or 1")
                    }, ep is { SeasonNumber: not null, Number: not null } ? $"S{ep.SeasonNumber:00}E{ep.Number:00}" : null,
                    series.Id,
                    ep.SeasonNumber > 0 ? currentEpiosdeNumber : null);

                if (ep.SeasonNumber > 0)
                    currentEpiosdeNumber++;

                return episode;
            })
        );
    }
}

public record TvdbApiErrorResponse(int Status, string Title, string Detail);