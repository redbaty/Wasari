using Wasari.Tvdb;
using Wasari.Tvdb.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResponseCaching();
builder.Services.AddTvdbServices();
builder.Services.AddOutputCache();
builder.Services.AddScoped<TvdbEpisodesService>();

var app = builder.Build();
app.UseOutputCache();
app.MapGet("/episodes", (TvdbEpisodesService tvdbEpisodesService, string query) => tvdbEpisodesService.GetEpisodes(query))
    .CacheOutput(o => o.Expire(TimeSpan.FromMinutes(5))
        .SetVaryByQuery(Array.Empty<string>())
        .VaryByValue(context =>
        {
            var value = context.Request.Query["query"].ToString().Trim();
            return new KeyValuePair<string, string>("query", value);
        }));

app.Run();