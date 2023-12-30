using System.ComponentModel;
using CliWrap;
using CliWrap.Buffered;
using Wasari.App.Abstractions;

namespace WasariEnvironment.Finders;

internal abstract class BaseFeatureFinder
{
    protected static async Task<bool> IsProgramAvailable(string exeName, string? arguments)
    {
        var command = Cli
            .Wrap(exeName)
            .WithValidation(CommandResultValidation.ZeroExitCode);

        if (!string.IsNullOrEmpty(arguments))
            command = command.WithArguments(arguments);

        var resultado = await command.ExecuteAsync();
        return resultado.ExitCode == 0;
    }
    
    protected async Task<EnvironmentFeature?> GetProgramWithVersion(string executable, string arguments,
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
}