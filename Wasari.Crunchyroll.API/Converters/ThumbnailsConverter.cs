using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Flurl;

namespace Wasari.Crunchyroll.API.Converters
{
    internal class ThumbnailsConverter : JsonConverter<string[]>
    {
        public override string[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var jsonNode = JsonNode.Parse(ref reader);
            var thumbnailsRootArray = jsonNode?["thumbnail"]?.AsArray();

            if (thumbnailsRootArray != null)
            {
                var thumbnailsSubArray = thumbnailsRootArray.Single().AsArray();
                var strings = thumbnailsSubArray.Select(i => i["source"]?.GetValue<string>())
                    .Where(i => !string.IsNullOrEmpty(i))
                    .Select(i => Url.ParsePathSegments(i).Last())
                    .Distinct()
                    .Select(Path.GetFileNameWithoutExtension)
                    .ToArray();

                return strings;
            }

            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, string[] value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}