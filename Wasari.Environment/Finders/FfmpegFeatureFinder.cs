using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;
using WasariEnvironment.Extensions;

namespace WasariEnvironment.Finders;

internal partial class FfmpegFeatureFinder : BaseFeatureFinder, IEnvironmentFeatureFinder
{
    private static async Task<bool> IsLibPlaceboAvailable(EnvironmentFeature ffmpeg)
    {
        var command = Cli
            .Wrap(ffmpeg.Path)
            .WithArguments("-version")
            .WithValidation(CommandResultValidation.ZeroExitCode);
        var bufferedCommandResult = await command.ExecuteBufferedAsync();

        return bufferedCommandResult.StandardOutput.Contains("--enable-libplacebo");
    }

    private static IEnumerable<EnvironmentFeatureModule> ParseFfmpegModules(string input)
    {
        foreach (Match match in ModuleRegex().Matches(input))
        {
            if (!match.Success)
                continue;

            var name = match.Groups["Name"].Value;
            var versions = new[]
                { Version.Parse(match.Groups["Version1"].Value), Version.Parse(match.Groups["Version2"].Value) };

            yield return new EnvironmentFeatureModule(name, versions.Max());
        }
    }
    
    public async Task<ICollection<EnvironmentFeature>> GetFeaturesAsync()
    {
        var featuresToReturn = new HashSet<EnvironmentFeature>();

        if (await GetProgramWithVersion(Environment.GetEnvironmentVariable("FFMPEG") ?? "ffmpeg", "-version",
                EnvironmentFeatureType.Ffmpeg, null, s => ParseFfmpegModules(s).ToArray()).DefaultIfFailed() is
            { } ffmpegFeature)
        {
            featuresToReturn.Add(ffmpegFeature);

            if (await IsLibPlaceboAvailable(ffmpegFeature))
                featuresToReturn.Add(new EnvironmentFeature(EnvironmentFeatureType.FfmpegLibPlacebo, null, null, string.Empty));
        }

        return featuresToReturn;
    }

    [GeneratedRegex(@"(?<Name>\w+) +(?<Version1>[0-9 ]+\.[0-9 ]+\.[0-9 ]+)\/(?<Version2>[0-9 ]+\.[0-9 ]+\.[0-9 ]+)")]
    private static partial Regex ModuleRegex();
}