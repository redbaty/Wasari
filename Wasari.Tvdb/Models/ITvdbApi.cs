namespace Wasari.Tvdb.Models;

public interface ITvdbApi
{
    Task<TvdbResponse<IReadOnlyList<TvdbSearchResponseSeries>>?> SearchAsync(string query, string type = "series");
    
    Task<TvdbResponse<TvdbSeries>?> GetSeriesAsync(string id, string seasonType = "default", string lang = "eng",
        int page = 0);
}