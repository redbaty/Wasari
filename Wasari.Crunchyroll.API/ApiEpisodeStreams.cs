using System.Text.Json.Serialization;
using Wasari.Crunchyroll.API.Converters;

namespace Wasari.Crunchyroll.API;

public class ApiEpisodeStreams
{
    [JsonPropertyName("media_id")]
    public string Id { get; init; }
        
    [JsonPropertyName("subtitles")]
    [JsonConverter(typeof(SubtitlesConverter))]
    public ApiEpisodeStreamSubtitle[] Subtitles { get; init; }
        
    [JsonPropertyName("streams")]
    [JsonConverter(typeof(StreamsConverter))]
    public ApiEpisodeStreamLink[] Streams { get; init; }
    
    [JsonPropertyName("audio_locale")]
    public string AudioLocale { get; init; }
}