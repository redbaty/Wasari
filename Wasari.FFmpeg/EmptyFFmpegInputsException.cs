using Wasari.App.Abstractions;

namespace Wasari.FFmpeg;

public class EmptyFFmpegInputsException : Exception
{
    public EmptyFFmpegInputsException(IWasariEpisode episode) : base($"No valid FFmpeg input found for episode {episode.Number} of season {episode.SeasonNumber}")
    {
    }
}