using System;

namespace Wasari.Exceptions
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