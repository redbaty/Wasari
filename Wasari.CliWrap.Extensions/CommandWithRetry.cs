using System.Text;
using CliWrap;
using CliWrap.EventStream;
using Microsoft.Extensions.Logging;

namespace Wasari.CliWrap.Extensions
{
    public class CommandWithRetry
    {
        public int RetryCount { get; }

        private int CurrentCount { get; set; }

        public Command Command { get; }

        internal TimeSpan Timeout { get; }

        internal ILogger? Logger { get; }
        
        internal Action<CommandEvent>? OnCommandEvent { get; }

        private string Name { get; }

        public CommandWithRetry(int retryCount, Command command, TimeSpan timeout, ILogger? logger, Action<CommandEvent>? onCommandEvent)
        {
            RetryCount = retryCount;
            Command = command;
            Timeout = timeout;
            Logger = logger;
            OnCommandEvent = onCommandEvent;
            Name = Path.GetFileNameWithoutExtension(command.TargetFilePath);
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
                    if (commandEvent is ExitedCommandEvent exitedCommandEvent)
                    {
                        if (exitedCommandEvent.ExitCode == 0)
                        {
                            return;
                        }

                        await Task.Delay(Timeout);
                        CurrentCount++;
                        Logger?.LogError("'{@Command}' failed for the {@CurrentRetryCount} time, {@StdErr}", Command.ToString(), CurrentCount, stdOutputBuilder.ToString());
                    }
                    else if (commandEvent is StandardErrorCommandEvent standardErrorCommandEvent)
                    {
                        stdOutputBuilder.AppendLine(standardErrorCommandEvent.Text);
                        Logger?.LogTrace("[{@Program}][STDERR] {@Text}", Name, standardErrorCommandEvent.Text);
                    }
                    else if (commandEvent is StandardOutputCommandEvent standardOutputCommandEvent)
                    {
                        stdErrBuilder.AppendLine(standardOutputCommandEvent.Text);
                        Logger?.LogTrace("[{@Program}][STDOUT] {@Text}", Name, standardOutputCommandEvent.Text);
                    }
                    
                    OnCommandEvent?.Invoke(commandEvent);
                }
            }

            throw new CommandFailedException(this, stdOutputBuilder.ToString(), stdErrBuilder.ToString());
        }
    }
}