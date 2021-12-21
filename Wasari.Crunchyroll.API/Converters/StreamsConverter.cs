using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Wasari.Crunchyroll.API.Converters;

internal class StreamsConverter : JsonConverter<ApiEpisodeStreamLink[]>
{
    public override ApiEpisodeStreamLink[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var jsonNode = JsonNode.Parse(ref reader);
        var links = new List<ApiEpisodeStreamLink>();

        if (jsonNode != null)
            foreach (var (key, value) in jsonNode.AsObject())
            {
                var jsonObject = value.AsObject();

                foreach (var (locale, childNode) in jsonObject)
                {
                    var url = childNode["url"]?.GetValue<string>();

                    if (!string.IsNullOrEmpty(url))
                    {
                        links.Add(new ApiEpisodeStreamLink
                        {
                            Url = url,
                            Type = key,
                            Locale = locale
                        });
                    }
                }
            }

        return links.ToArray();
    }

    public override void Write(Utf8JsonWriter writer, ApiEpisodeStreamLink[] value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}