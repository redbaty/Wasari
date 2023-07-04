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
            .Where(i => i.Status != DownloadedEpisodeStatus.AlreadyExists)
            .GroupBy(i => new { i.Episode.SeriesName, Status = i.Status })
            .Select(i =>
            {
                var sb = new StringBuilder();

                switch (i.Key.Status)
                {
                    case DownloadedEpisodeStatus.Downloaded:
                        sb.AppendLine($"Episodes has been downloaded for series: {i.Key.SeriesName}");
                        break;
                    case DownloadedEpisodeStatus.Failed:
                        sb.AppendLine($"Failed to download episodes for series: {i.Key.SeriesName}");
                        break;
                }

                foreach (var episode in i)
                {
                    sb.AppendLine($"{episode.Episode.Prefix} - {episode.Episode.Title}");
                }

                return sb.ToString();
            })
            .DefaultIfEmpty()
            .Aggregate((x, y) => $"{x}{Environment.NewLine}{y}");

        if (!string.IsNullOrEmpty(message))
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