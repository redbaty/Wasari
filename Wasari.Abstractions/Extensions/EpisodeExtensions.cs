using System.IO;

namespace Wasari.Abstractions.Extensions;

public static class EpisodeExtensions
{
    private static string FinalEpisodeFileExtension(DownloadParameters downloadParameters)
    {
        if (downloadParameters.UseHevc || downloadParameters.Subtitles)
        {
            return ".mkv";
        }

        return ".mp4";
    }

        
    public static string FinalEpisodeFile(this IEpisodeInfo episode, DownloadParameters downloadParameters)
    {
        var fileName = string.Format(downloadParameters.FileMask ?? "{0} - {1}", episode.FilePrefix, episode.Name.AsSafePath());
        var finalEpisodeFileName = $"{fileName}{FinalEpisodeFileExtension(downloadParameters)}";
        var outputDirectory = downloadParameters.FinalOutputDirectory(episode.SeriesInfo.Name);

        return downloadParameters.CreateSeasonFolder
            ? Path.Combine(
                outputDirectory,
                $"Season {episode.SeasonInfo.Season}",
                finalEpisodeFileName)
            : Path.Combine(
                outputDirectory,
                finalEpisodeFileName);
    }
}