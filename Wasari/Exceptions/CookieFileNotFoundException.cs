using System.IO;

namespace Wasari.Exceptions
{
    public sealed class CookieFileNotFoundException : FileNotFoundException
    {
        public CookieFileNotFoundException(string path) : base("Cookie file was not found", path)
        {
        }
    }
}