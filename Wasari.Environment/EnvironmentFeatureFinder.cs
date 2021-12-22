using System.ComponentModel;
using CliWrap;
using CliWrap.Buffered;

namespace WasariEnvironment;

public static class EnvironmentFeatureFinder
{
    private static Task<bool> IsProgramAvailable(string exeName, string? arguments)
    {
        var command = Cli
            .Wrap(exeName)
            .WithValidation(CommandResultValidation.ZeroExitCode);

        if (!string.IsNullOrEmpty(arguments))
            command = command.WithArguments(arguments);

        return command.ExecuteAsync().Task.ContinueWith(t =>
        {
            if (!t.IsCompletedSuccessfully)
            {
                if (t.Exception?.InnerException is Win32Exception win32Exception &&
                    win32Exception.Message.EndsWith("The system cannot find the file specified."))
                {
                    return false;
                }

                throw t.Exception ??
                      throw new InvalidOperationException(
                          "An unexpected erro occurred while scanning environment features");
            }

            return t.Result.ExitCode == 0;
        });
    }

    private static async Task<bool> IsLibPlaceboAvailable()
    {
        var command = Cli
            .Wrap("ffmpeg")
            .WithArguments("-version")
            .WithValidation(CommandResultValidation.ZeroExitCode);
        var bufferedCommandResult = await command.ExecuteBufferedAsync();

        return bufferedCommandResult.StandardOutput.Contains("--enable-libplacebo");
    }

    public static async IAsyncEnumerable<EnvironmentFeature> GetEnvironmentFeatures()
    {
        if (await IsProgramAvailable("yt-dlp", "--version").DefaultsToFalse())
            yield return EnvironmentFeature.YtDlp;

        if (await IsProgramAvailable("ffmpeg", "-version").DefaultsToFalse())
        {
            yield return EnvironmentFeature.Ffmpeg;
            
            if (await IsLibPlaceboAvailable())
                yield return EnvironmentFeature.FfmpegLibPlacebo;
        }

        if (await IsProgramAvailable("nvidia-smi", null).DefaultsToFalse())
            yield return EnvironmentFeature.NvidiaGpu;
    }
}