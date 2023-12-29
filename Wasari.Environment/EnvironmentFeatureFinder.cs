using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;
using LibreHardwareMonitor.Hardware;
using Wasari.App.Abstractions;

namespace WasariEnvironment;

public static class EnvironmentFeatureFinder
{
    private static async Task<EnvironmentFeature?> GetProgramWithVersion(string executable, string arguments,
        EnvironmentFeatureType type,
        string? versionRegex = null,
        Func<string, EnvironmentFeatureModule[]>? modulesParser = null)
    {
        var command = Cli
            .Wrap(executable)
            .WithValidation(CommandResultValidation.ZeroExitCode);

        if (!string.IsNullOrEmpty(arguments))
            command = command.WithArguments(arguments);

        var executionResult = await command.ExecuteBufferedAsync()
            .Task
            .ContinueWith(t =>
            {
                if (!t.IsCompletedSuccessfully)
                {
                    if (t.Exception?.InnerException is Win32Exception win32Exception &&
                        win32Exception.Message.EndsWith("The system cannot find the file specified."))
                        return null;

                    throw t.Exception ??
                          throw new InvalidOperationException(
                              "An unexpected error occurred while scanning environment features");
                }


                return t.Result.ExitCode == 0 ? t.Result.StandardOutput : null;
            });

        if (string.IsNullOrEmpty(executionResult))
            return null;
        executionResult = executionResult.Trim();

        Version? mainVersion = null;
        EnvironmentFeatureModule[]? modules = null;

        if (!string.IsNullOrEmpty(versionRegex))
            if (executionResult.GetValueFromRegex<string>(versionRegex, out var version) &&
                Version.TryParse(version, out var localVersion))
                mainVersion = localVersion;

        if (modulesParser != null) modules = modulesParser.Invoke(executionResult);

        return new EnvironmentFeature(type, mainVersion, modules, executable);
    }

    private static async Task<bool> IsProgramAvailable(string exeName, string? arguments)
    {
        var command = Cli
            .Wrap(exeName)
            .WithValidation(CommandResultValidation.ZeroExitCode);

        if (!string.IsNullOrEmpty(arguments))
            command = command.WithArguments(arguments);

        var resultado = await command.ExecuteAsync();
        return resultado.ExitCode == 0;
    }

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
        foreach (Match match in Regex.Matches(input,
                     @"(?<Name>\w+) +(?<Version1>[0-9 ]+\.[0-9 ]+\.[0-9 ]+)\/(?<Version2>[0-9 ]+\.[0-9 ]+\.[0-9 ]+)"))
        {
            if (!match.Success)
                continue;

            var name = match.Groups["Name"].Value;
            var versions = new[]
                { Version.Parse(match.Groups["Version1"].Value), Version.Parse(match.Groups["Version2"].Value) };

            yield return new EnvironmentFeatureModule(name, versions.Max());
        }
    }

    private static async Task<ICollection<EnvironmentFeature>> FindGpus()
    {
        var computer = new Computer
        {
            IsGpuEnabled = true
        };

        try
        {
            var features = new HashSet<EnvironmentFeatureType>();
            
            computer.Open();
            
            foreach (var hardware in computer.Hardware)
            {
                switch (hardware.HardwareType)
                {
                    case HardwareType.GpuNvidia:
                        if (await IsProgramAvailable("nvidia-smi", null).DefaultIfFailed())
                            features.Add(EnvironmentFeatureType.NvidiaGpu);
                        
                        break;
                    case HardwareType.GpuAmd:
                        features.Add(EnvironmentFeatureType.AmdGpu);
                        break;
                }
            }
            
            return features
                .Select(i => new EnvironmentFeature(i, null, null, string.Empty))
                .ToHashSet();
        }
        catch (Exception e)
        {
            Debug.WriteLine("Failed to open LibreHardwareMonitor: {0}", e);
            return Array.Empty<EnvironmentFeature>();
        }
        finally
        {
            computer.Close();
        }
    }

    public static async IAsyncEnumerable<EnvironmentFeature> GetEnvironmentFeatures()
    {
        if (await GetProgramWithVersion(Environment.GetEnvironmentVariable("YTDLP") ?? "yt-dlp", "--version",
                EnvironmentFeatureType.YtDlp, "\\d+\\.\\d+\\.\\d+").DefaultIfFailed() is
            { } ytDlpFeature)
            yield return ytDlpFeature;

        if (await GetProgramWithVersion(Environment.GetEnvironmentVariable("FFMPEG") ?? "ffmpeg", "-version",
                EnvironmentFeatureType.Ffmpeg, null, s => ParseFfmpegModules(s).ToArray()).DefaultIfFailed() is
            { } ffmpegFeature)
        {
            yield return ffmpegFeature;

            if (await IsLibPlaceboAvailable(ffmpegFeature))
                yield return new EnvironmentFeature(EnvironmentFeatureType.FfmpegLibPlacebo, null, null, string.Empty);
        }

        foreach (var gpuFeature in await FindGpus())
            yield return gpuFeature;
    }
}