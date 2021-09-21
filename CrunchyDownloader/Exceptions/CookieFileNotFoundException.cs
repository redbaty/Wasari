using System.IO;

namespace CrunchyDownloader.Exceptions
{
    public sealed class CookieFileNotFoundException : FileNotFoundException
    {
        public CookieFileNotFoundException(string path) : base("Cookie file was not found", path)
        {
        }
    }
}