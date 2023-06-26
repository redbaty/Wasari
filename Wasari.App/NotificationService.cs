using System.Net.Http.Json;
using System.Text;
using Wasari.App.Abstractions;

namespace Wasari.App;

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
            .GroupBy(i => new { i.Episode.SeriesName, i.Success })
            .Select(i =>
            {
                var sb = new StringBuilder();

                if (i.Key.Success)
                    sb.AppendLine($"Episodes has been downloaded for series: {i.Key.SeriesName}");
                else
                    sb.AppendLine($"Failed to download episodes for series: {i.Key.SeriesName}");

                foreach (var episode in i)
                {
                    sb.AppendLine($"{episode.Episode.Prefix} - {episode.Episode.Title}");
                }

                return sb.ToString();
            })
            .Aggregate((x, y) => $"{x}{Environment.NewLine}{y}");
        await SendNotificationAsync(message);
    }

    private async ValueTask SendNotificationAsync(string message)
    {
        await HttpClient.PostAsJsonAsync(string.Empty, new
        {
            content = message
        });
    }
}