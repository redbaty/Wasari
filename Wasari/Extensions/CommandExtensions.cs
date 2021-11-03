using System;
using CliWrap;
using CliWrap.EventStream;
using Microsoft.Extensions.Logging;
using Wasari.App;

namespace Wasari.Extensions
{
    internal static class CommandExtensions
    {
        public static CommandWithRetry WithRetryCount(this Command command, int retryCount, TimeSpan? timeOut = null, ILogger logger = null, Action<CommandEvent> onCommandEvent = null) => new(retryCount, command, timeOut ?? TimeSpan.FromSeconds(5), logger, onCommandEvent);
        
        public static CommandWithRetry WithLogger(this CommandWithRetry command, ILogger logger) => new(command.RetryCount, command.Command, command.Timeout, logger, command.OnCommandEvent);
        
        public static CommandWithRetry WithCommandHandler(this CommandWithRetry command, Action<CommandEvent> onCommandEvent) => new(command.RetryCount, command.Command, command.Timeout, command.Logger, onCommandEvent);
    }
}