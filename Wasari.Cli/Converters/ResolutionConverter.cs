using System.Text.RegularExpressions;
using CliFx.Extensibility;
using Microsoft.Extensions.Options;
using Wasari.FFmpeg;

namespace Wasari.Cli.Converters;

public class ResolutionConverter : BindingConverter<FFmpegResolution?>
{
    public ResolutionConverter(IOptions<FFmpegResolutionPresets> options)
    {
        Options = options;
    }

    private IOptions<FFmpegResolutionPresets> Options { get; }

    public override FFmpegResolution? Convert(string? rawValue)
    {
        if (string.IsNullOrEmpty(rawValue))
            return null;

        var match = Regex.Match(rawValue, @"(?<Width>\d+)[*|x|X](?<Height>\d+)");
        if (match.Success) return new FFmpegResolution(int.Parse(match.Groups["Width"].Value), int.Parse(match.Groups["Height"].Value));

        if (Options.Value.Presets.TryGetValue(rawValue, out var resolution)) return resolution;

        throw new Exception("Invalid resolution provided");
    }
}