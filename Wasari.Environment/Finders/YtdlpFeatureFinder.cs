using WasariEnvironment.Extensions;

namespace WasariEnvironment.Finders;

internal class YtdlpFeatureFinder : BaseFeatureFinder, IEnvironmentFeatureFinder
{
    public async Task<ICollection<EnvironmentFeature>> GetFeaturesAsync()
    {
        var featuresToReturn = new HashSet<EnvironmentFeature>();

        if (await GetProgramWithVersion(Environment.GetEnvironmentVariable("YTDLP") ?? "yt-dlp", "--version",
                EnvironmentFeatureType.YtDlp, @"\d+\.\d+\.\d+").DefaultIfFailed() is
            { } ytDlpFeature)
            featuresToReturn.Add(ytDlpFeature);

        return featuresToReturn;
    }
}