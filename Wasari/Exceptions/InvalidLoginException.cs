using System;

namespace Wasari.Exceptions
{
    public sealed class InvalidLoginException : Exception
    {
        internal InvalidLoginException(string username, string password) : base($"Failed to login in crunchyroll using username: {username} and password: {password}")
        {
            
        }
    }
}