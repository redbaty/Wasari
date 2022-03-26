using System;
using System.IO;

namespace Wasari.Cli.Models
{
    public class TemporaryCookieFile : IDisposable
    {
        public string Path { get; init; }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (File.Exists(Path))
                File.Delete(Path);
        }
    }
}