using Wasari.Tvdb.Abstractions;

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
        
        if(searchResult.Data.Count != 1)
            return Results.BadRequest(new
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid query",
                Detail = "Query must return exactly one result"
            });
        
        var series = searchResult.Data.Single();
        var seriesWithEpisodes = await TvdbApi.GetSeriesAsync(series.TvdbId);

        return Results.Ok(seriesWithEpisodes.Data.Episodes
            .Where(i => !string.IsNullOrEmpty(i.Name))
            .Select(i => new Episode(i.Name, i.SeasonNumber, i.Number, i.IsMovie switch
            {
                0 => false,
                1 => true,
                _ => throw new ArgumentException("IsMovie flag is not 0 or 1")
            }, i is { SeasonNumber: not null, Number: not null } ? $"S{i.SeasonNumber:00}E{i.Number:00}" : null)));
    }
}