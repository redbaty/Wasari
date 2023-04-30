using System.Net.Http.Json;
using System.Text;
using Wasari.App.Abstractions;

namespace Wasari.Cli.Services;

public class NotificationService
{
    public NotificationService(HttpClient httpClient)
    {
        HttpClient = httpClient;
    }

    private HttpClient HttpClient { get; }
    
    public async ValueTask SendNotifcationForDownloadedEpisodeAsync(IEnumerable<DownloadedEpisode> downloadedEpisode)
    {
        var message = downloadedEpisode
            .GroupBy(i => i.Episode.SeriesName)
            .Select(i =>
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Episodes has been downloaded for series: {i.Key}");

                foreach (var episode in i)
                {
                    sb.AppendLine($"{episode.Episode.Prefix} - {episode.Episode.Title}");
                }

                return sb.ToString();
            })
            .Aggregate((x, y) => $"{x}{Environment.NewLine}{y}");
        await SendNotificationAsync(message);
    }

    public async ValueTask SendNotificationAsync(string message)
    {
        await HttpClient.PostAsJsonAsync(string.Empty, new
        {
            content = message
        });
    }
}