using System.Text.RegularExpressions;

namespace Wasari.App;

public static class StringExtensions
{
    private static readonly Regex RemoveInvalidChars = new($"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]",
        RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string AsSafePath(this string fileOrDirectoryName)
    {
        return RemoveInvalidChars.Replace(fileOrDirectoryName, string.Empty);
    }
}