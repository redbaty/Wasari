using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace Wasari.Abstractions.Extensions
{
    public static class LoggingExtensions
    {
        private static Dictionary<string, int> Maxes { get; } = new();

        public static T? ObjectFromLogEvent<T>(this LogEvent logEvent) where T : new()
        {
            if (logEvent.Properties.TryGetValue("ProgressUpdate", out var property) &&
                property is StructureValue structureValue)
            {
                var item = new T();

                foreach (var structureValueProperty in structureValue.Properties)
                {
                    var propertyInfo = typeof(ProgressUpdate).GetProperty(structureValueProperty.Name);

                    if (propertyInfo != null && propertyInfo.CanWrite &&
                        structureValueProperty.Value is ScalarValue scalarValue)
                    {
                        propertyInfo.SetValue(item, scalarValue.Value);
                    }
                }

                return item;
            }

            return default;
        }

        public static void LogProgressUpdate(this ILogger logger, ProgressUpdate progressUpdate)
        {
            if (progressUpdate.Type == ProgressUpdateTypes.Max && !string.IsNullOrEmpty(progressUpdate.EpisodeId))
            {
                if (Maxes.ContainsKey(progressUpdate.EpisodeId))
                    Maxes[progressUpdate.EpisodeId] = progressUpdate.Value;
                else
                    Maxes.Add(progressUpdate.EpisodeId, progressUpdate.Value);
            }

            if (progressUpdate.Type == ProgressUpdateTypes.Completed && !string.IsNullOrEmpty(progressUpdate.EpisodeId)) Maxes.Remove(progressUpdate.EpisodeId);

            var currentMax = string.IsNullOrEmpty(progressUpdate.EpisodeId) ? 0 : Maxes.GetValueOrDefault(progressUpdate.EpisodeId);
            var currentPercentage = currentMax <= 0 ? 0 : progressUpdate.Value / currentMax;
            logger.LogInformation(progressUpdate.Id,
                "[Progress Update][{@Id}][{@Type}][{@CurrentPercentage}] Progress update value: {@Value}. {@ProgressUpdate}",
                progressUpdate.EpisodeId, progressUpdate.Type, currentPercentage, progressUpdate.Value, progressUpdate);
        }
    }
}