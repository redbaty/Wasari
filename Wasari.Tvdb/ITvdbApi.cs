using Refit;
using Wasari.Tvdb.Models;

namespace Wasari.Tvdb;

public interface ITvdbApi
{
    [Get("/search")]
    Task<TvdbResponse<IReadOnlyList<TvdbSearchResponseSeries>>> SearchAsync(string query, string type = "series");

    [Get("/series/{id}/episodes/{seasonType}/{lang}")]
    Task<TvdbResponse<TvdbSeries>> GetSeriesAsync(string id, string seasonType = "default", string lang = "eng", int page = 0);
    
    [Get("/series/{id}/episodes/{seasonType}/{lang}")]
    Task<string> GetSeriesRawAsync(string id, string seasonType = "default", string lang = "eng", int page = 0);
}