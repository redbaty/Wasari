using System.Text.RegularExpressions;

namespace Wasari.Tvdb.Api.Extensions;

internal static partial class StringExtensions
{
    [GeneratedRegex("[a-zA-Z0-9 ]+")]
    private static partial Regex EpisodeTitleNormalizeRegex();

    public static string NormalizeUsingRegex(this string str) => string.Join(string.Empty, EpisodeTitleNormalizeRegex().Matches(str).Select(o => o.Value));
}