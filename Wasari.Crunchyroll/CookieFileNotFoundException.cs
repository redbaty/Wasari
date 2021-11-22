using System.IO;

namespace Crunchyroll.API
{
    public sealed class CookieFileNotFoundException : FileNotFoundException
    {
        public CookieFileNotFoundException(string path) : base("Cookie file was not found", path)
        {
        }
    }
}