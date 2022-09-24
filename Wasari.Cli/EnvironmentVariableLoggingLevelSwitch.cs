using Serilog.Core;
using Serilog.Events;

namespace Wasari.Cli;

internal class EnvironmentVariableLoggingLevelSwitch : LoggingLevelSwitch
{
    public EnvironmentVariableLoggingLevelSwitch(string environmentVariable)
    {
        if (Enum.TryParse<LogEventLevel>(Environment.ExpandEnvironmentVariables(environmentVariable), true, out var level))
        {
            MinimumLevel = level;
        }
    }
}