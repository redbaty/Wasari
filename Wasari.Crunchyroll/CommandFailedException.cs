using System;

namespace Crunchyroll.API
{
    public sealed class CommandFailedException : Exception
    {
        internal CommandFailedException(CommandWithRetry commandWithRetry, string stdOut, string stdErr) : base($"Command did not execute even after {commandWithRetry.RetryCount} tries. StdOut: {stdOut} StdErr: {stdErr}")
        {
            Data.Add(nameof(stdOut), stdOut);
            Data.Add(nameof(stdErr), stdErr);
        }
    }
}