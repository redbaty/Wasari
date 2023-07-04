using Oakton;
using Oakton.Resources;
using Wasari.App;
using Wasari.App.Abstractions;
using Wasari.App.Extensions;
using Wasari.Crunchyroll;
using Wasari.Daemon.Extensions;
using Wasari.Daemon.Handlers;
using Wasari.Daemon.HostedServices;
using Wasari.Daemon.Models;
using Wasari.Daemon.Options;
using Wasari.FFmpeg;
using Wasari.Tvdb.Api.Client;
using Wasari.YoutubeDlp;
using WasariEnvironment;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.FluentValidation;
using Wolverine.Postgresql;

var outputDirectory = Environment.GetEnvironmentVariable("OUTPUT_DIRECTORY") ?? throw new InvalidOperationException("OUTPUT_DIRECTORY environment variable is not set");
var username = Environment.GetEnvironmentVariable("USERNAME") ?? throw new InvalidOperationException("USERNAME environment variable is not set");
var password = Environment.GetEnvironmentVariable("PASSWORD") ?? throw new InvalidOperationException("PASSWORD environment variable is not set");
var postgresCs = Environment.GetEnvironmentVariable("POSTGRESQL_CONNECTION_STRING") ?? throw new InvalidOperationException("POSTGRESQL_CONNECTION_STRING environment variable is not set");
var webhookUrl = Environment.GetEnvironmentVariable("WEBHOOK_URL");

var builder = WebApplication.CreateBuilder(args);

builder.Host.ApplyOaktonExtensions();
await builder.Services.AddEnvironmentServices();
builder.Services.AddHostedService<EnvironmentCheckerService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDownloadServices();

builder.Services.Configure<DownloadOptions>(o =>
{
    o.DefaultOutputDirectory = outputDirectory;
    o.IncludeDubs = false;
    o.IncludeSubs = true;
    o.SkipExistingFiles = true;
    o.CreateSeriesFolder = true;
    o.CreateSeasonFolder = true;
    o.TryEnrichEpisodes = true;
    o.SkipUniqueEpisodeCheck = false;
});
builder.Services.Configure<FFmpegOptions>(o =>
{
    o.UseHevc = false;
    o.UseNvidiaAcceleration = false;
    o.UseTemporaryEncodingPath = true;
    o.Shaders = null;
    o.Resolution = null;
    o.Threads = null;
});
builder.Services.Configure<AuthenticationOptions>(o =>
{
    o.Username = username;
    o.Password = password;
});
builder.Services.Configure<YoutubeDlpOptions>(c =>
{
    c.Format = null;
    c.IgnoreTls = false;
});
builder.Services.AddCrunchyrollServices();
builder.Services.AddMemoryCache();
builder.Services.AddWasariTvdbApi();

builder.Host.UseWolverine(opts =>
{
    opts.Durability.StaleNodeTimeout = TimeSpan.FromSeconds(10);

    opts.LocalQueueFor<DownloadRequest>()
        .Sequential()
        .UseDurableInbox();

    opts.Discovery.DisableConventionalDiscovery();
    opts.Discovery.IncludeType<DownloadRequestHandler>();

    opts.PersistMessagesWithPostgresql(postgresCs);
    opts.UseEntityFrameworkCoreTransactions();
    opts.Policies.AutoApplyTransactions();

    opts.Policies.UseDurableInboxOnAllListeners();
    opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
    opts.UseFluentValidation();
});

if (webhookUrl != null)
{
    builder.Services.AddHttpClient<NotificationService>(c => { c.BaseAddress = new Uri(webhookUrl); });
    builder.Services.Configure<NotificationOptions>(o =>
    {
        o.Enabled = true;
    });
}

builder.Host.UseResourceSetupOnStartup();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.ResetWolverine();
}

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
await app.RunOaktonCommands(args);