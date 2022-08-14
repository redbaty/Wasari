using System;
using System.IO;

namespace Wasari.Abstractions.Extensions;

public static class DownloadParameterExtensions
{
    public static string FinalOutputDirectory(this DownloadParameters downloadParameters, string? seriesName)
    {
        var outputDirectory = downloadParameters.BaseOutputDirectory ?? Environment.CurrentDirectory;

        if (downloadParameters.CreateSeriesFolder && !string.IsNullOrEmpty(seriesName))
        {
            outputDirectory = Path.Combine(outputDirectory, seriesName);
        }

        return outputDirectory;
    }
}