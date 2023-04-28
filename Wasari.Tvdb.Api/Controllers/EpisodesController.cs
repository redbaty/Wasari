using Microsoft.AspNetCore.Mvc;
using Wasari.Tvdb.Api.Services;

namespace Wasari.Tvdb.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class EpisodesController : ControllerBase
{
    public EpisodesController(TvdbEpisodesService tvdbEpisodesService)
    {
        TvdbEpisodesService = tvdbEpisodesService;
    }

    private TvdbEpisodesService TvdbEpisodesService { get; }

    [HttpGet]
    [ResponseCache(Duration = 60 * 15, VaryByQueryKeys = new[] { "query" })]
    public ValueTask<IResult> GetEpisodes(string query)
    {
        return TvdbEpisodesService.GetEpisodes(query);
    }
}