using Wasari.Tvdb;
using Wasari.Tvdb.Api.Policies;
using Wasari.Tvdb.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResponseCaching();
builder.Services.AddTvdbServices();
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy(nameof(EpisodeCachePolicy), EpisodeCachePolicy.Instance);
});
builder.Services.AddScoped<TvdbEpisodesService>();

var app = builder.Build();
app.UseOutputCache();
app.MapGet("/episodes", (TvdbEpisodesService tvdbEpisodesService, string query) => tvdbEpisodesService.GetEpisodes(query))
    .CacheOutput(nameof(EpisodeCachePolicy));

app.Run();