using System;
using System.Text;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.EventStream;
using Microsoft.Extensions.Logging;

namespace Crunchyroll.API
{
    internal class CommandWithRetry
    {
        public int RetryCount { get; }

        private int CurrentCount { get; set; }

        public Command Command { get; }

        internal TimeSpan Timeout { get; }

        internal ILogger Logger { get; }
        
        internal Action<CommandEvent> OnCommandEvent { get; }

        public CommandWithRetry(int retryCount, Command command, TimeSpan timeout, ILogger logger, Action<CommandEvent> onCommandEvent)
        {
            RetryCount = retryCount;
            Command = command;
            Timeout = timeout;
            Logger = logger;
            OnCommandEvent = onCommandEvent;
        }

        public async Task Execute()
        {
            var stdOutputBuilder = new StringBuilder();
            var stdErrBuilder = new StringBuilder();

            while (CurrentCount < RetryCount)
            {
                stdOutputBuilder.Clear();
                stdErrBuilder.Clear();
                
                await foreach (var commandEvent in Command.WithValidation(CommandResultValidation.None).ListenAsync(Encoding.UTF8))
                {
                    OnCommandEvent?.Invoke(commandEvent);
                    
                    if (commandEvent is ExitedCommandEvent exitedCommandEvent)
                    {
                        if (exitedCommandEvent.ExitCode == 0)
                        {
                            return;
                        }

                        await Task.Delay(Timeout);
                        CurrentCount++;
                        Logger?.LogDebug("'{@Command}' failed for the {@CurrentRetryCount} time", Command.ToString(), CurrentCount);
                    }
                    else if (commandEvent is StandardErrorCommandEvent standardErrorCommandEvent)
                    {
                        stdOutputBuilder.AppendLine(standardErrorCommandEvent.Text);
                        Logger?.LogTrace("[StdErr] {@Text}", standardErrorCommandEvent.Text);
                    }
                    else if (commandEvent is StandardOutputCommandEvent standardOutputCommandEvent)
                    {
                        stdErrBuilder.AppendLine(standardOutputCommandEvent.Text);
                        Logger?.LogTrace("[StdOut] {@Text}", standardOutputCommandEvent.Text);
                    }
                }
            }

            throw new CommandFailedException(this, stdOutputBuilder.ToString(), stdErrBuilder.ToString());
        }
    }
}