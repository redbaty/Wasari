using System.Globalization;
using System.Text.RegularExpressions;

namespace Wasari.App.Abstractions;

public static class StringExtensions
{
    public static bool GetValueFromRegex<T>(this string input, string regex, out T? @out)
    {
        if (typeof(T).IsArray)
        {
            var canParseFromRegex = GetValuesFromRegex(input, regex, out var value);

            if (canParseFromRegex && value != null)
            {
                if (typeof(T) == typeof(string[]))
                {
                    @out = (T)Convert.ChangeType(value.ToArray(), typeof(T));
                    return true;
                }
            }
        }
        else
        {
            var canParseFromRegex = GetValueFromRegex(input, regex, out var value);

            if (canParseFromRegex && !string.IsNullOrEmpty(value))
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
        }

        @out = default;
        return false;
    }

    private static bool GetValuesFromRegex(this string input, string regex, out List<string>? @out)
    {
        @out = new List<string>();

        if (!string.IsNullOrEmpty(input))
        {
            foreach (Match match in Regex.Matches(input, regex))
            {
                if (match.Success)
                    @out.Add(match.Value);
            }

            return true;
        }

        @out = null;
        return false;
    }

    private static bool GetValueFromRegex(this string input, string regex, out string? @out)
    {
        if (!string.IsNullOrEmpty(input) && Regex.Match(input, regex) is { Success: true } match)
        {
            @out = match.Groups.Count == 1 ? match.Groups[0].Value : match.Groups[1].Value;
            return true;
        }

        @out = null;
        return false;
    }
}