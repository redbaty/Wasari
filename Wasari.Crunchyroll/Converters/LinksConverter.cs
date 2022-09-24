using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Wasari.Crunchyroll.Converters;

internal class LinksConverter : JsonConverter<string[]>
{
    public override string[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var jsonNode = JsonNode.Parse(ref reader);
        var links = new List<string>();

        if (jsonNode != null)
            foreach (var (_, value) in jsonNode.AsObject())
            {
                var jsonObject = value.AsObject();
                if (jsonObject.ContainsKey("href"))
                {
                    var link = jsonObject["href"]?.GetValue<string>();

                    if (!string.IsNullOrEmpty(link))
                        links.Add(link);
                }
            }

        return links.ToArray();
    }

    public override void Write(Utf8JsonWriter writer, string[] value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}