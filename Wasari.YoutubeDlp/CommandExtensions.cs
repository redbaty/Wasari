using CliWrap;
using CliWrap.Buffered;

namespace Wasari.YoutubeDlp;

internal static class CommandExtensions
{
    public static async Task<string> ExecuteAndGetStdOut(this Command command)
    {
        var commandResult = await command.ExecuteBufferedAsync();
        return commandResult.StandardOutput;
    }
}