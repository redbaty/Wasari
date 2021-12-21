using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Wasari.Crunchyroll.API.Converters;

internal class SubtitlesConverter : JsonConverter<ApiEpisodeStreamSubtitle[]>
{
    public override ApiEpisodeStreamSubtitle[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var jsonNode = JsonNode.Parse(ref reader);
        var links = new List<ApiEpisodeStreamSubtitle>();

        if (jsonNode != null)
            foreach (var (key, value) in jsonNode.AsObject())
            {
                var jsonObject = value.AsObject();
                if (jsonObject.ContainsKey("url"))
                {
                    var link = jsonObject["url"]?.GetValue<string>();
                    var format = jsonObject["format"]?.GetValue<string>();

                    if (!string.IsNullOrEmpty(link))
                        links.Add(new ApiEpisodeStreamSubtitle
                        {
                            Url = link,
                            Locale = key,
                            Format = format
                        });
                }
            }

        return links.ToArray();
    }

    public override void Write(Utf8JsonWriter writer, ApiEpisodeStreamSubtitle[] value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}