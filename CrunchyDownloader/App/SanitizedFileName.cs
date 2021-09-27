using System.IO;
using System.Text.RegularExpressions;

namespace CrunchyDownloader.App
{
    public class SanitizedFileName
    {
        private static readonly Regex RemoveInvalidChars = new($"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]",
            RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public SanitizedFileName(string fileName, string replacement = "_")
        {
            Value = RemoveInvalidChars.Replace(fileName, replacement);
        }

        public string Value { get; }

        public override string ToString()
        {
            return Value;
        }
    }
}