using System.ComponentModel;
using CliWrap;

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

    public static async IAsyncEnumerable<EnvironmentFeature> GetEnvironmentFeatures()
    {
        if (await IsProgramAvailable("yt-dlp", "--version"))
            yield return EnvironmentFeature.YtDlp;

        if (await IsProgramAvailable("ffmpeg", "-version"))
            yield return EnvironmentFeature.Ffmpeg;

        if (await IsProgramAvailable("nvidia-smi", null))
            yield return EnvironmentFeature.NvidiaGpu;
    }
}