using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace CrunchyDownloader.Extensions
{
    internal static class StringExtensions
    {
        private static readonly Regex RemoveInvalidChars = new($"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]",
            RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        
        public static string AsSafePath(this string fileOrDirectoryName)
        {
            return RemoveInvalidChars.Replace(fileOrDirectoryName, string.Empty);
        }
        
        public static bool GetValueFromRegex<T>(this string input, string regex, out T @out)
        {
            var canParseFromRegex = GetValueFromRegex(input, regex, out var value);

            if (canParseFromRegex)
            {
                if (typeof(T) == typeof(double))
                {
                    if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                    {
                        @out = (T)Convert.ChangeType(d, typeof(T));
                        return true;
                    }
                }
                else if (typeof(T) == typeof(int))
                {
                    if (int.TryParse(value, NumberStyles.Any, null, out var i))
                    {
                        @out = (T)Convert.ChangeType(i, typeof(T));
                        return true;
                    }
                }
                else if (typeof(T) == typeof(string))
                {
                    @out = (T)Convert.ChangeType(value, typeof(T));
                    return true;
                }
            }

            @out = default;
            return false;
        }

        private static bool GetValueFromRegex(this string input, string regex, out string @out)
        {
            if (!string.IsNullOrEmpty(input) && Regex.Match(input, regex) is { Success: true } match)
            {
                @out = match.Groups[1].Value;
                return true;
            }

            @out = null;
            return false;
        }
    }
}