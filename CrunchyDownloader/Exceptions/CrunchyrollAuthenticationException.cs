using System;

namespace CrunchyDownloader.Exceptions
{
    public sealed class CrunchyrollAuthenticationException : Exception
    {
        internal CrunchyrollAuthenticationException(string message, string username, string password) : base(message)
        {
            Data.Add(nameof(username), username);
            Data.Add(nameof(password), password);
        }
    }
}