using WasariEnvironment;

namespace Wasari.Daemon.HostedServices;

public class EnvironmentCheckerService : IHostedService
{
    public EnvironmentCheckerService(EnvironmentService environmentService, ILogger<EnvironmentCheckerService> logger)
    {
        EnvironmentService = environmentService;
        Logger = logger;
    }

    private EnvironmentService EnvironmentService { get; }

    private ILogger<EnvironmentCheckerService> Logger { get; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var missingFeatures = EnvironmentService.GetMissingFeatures(EnvironmentFeatureType.Ffmpeg, EnvironmentFeatureType.YtDlp).ToArray();

        if (missingFeatures.Any())
        {
            Logger.LogError("Missing features: {MissingFeatures}", missingFeatures);
            throw new Exception($"Missing features: {string.Join(", ", missingFeatures)}");
        }

        var ffmpeg = EnvironmentService.GetFeature(EnvironmentFeatureType.Ffmpeg);

        if (ffmpeg is not null)
            Logger.LogInformation("Using FFmpeg version {Version}", ffmpeg.Value.Version);
        
        var ytDlp = EnvironmentService.GetFeature(EnvironmentFeatureType.YtDlp);
        
        if (ytDlp is not null)
            Logger.LogInformation("Using yt-dlp version {Version}", ytDlp.Value.Version);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}