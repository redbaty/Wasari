using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace Wasari.Abstractions.Extensions
{
    public static class LoggingExtensions
    {
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
            logger.LogInformation(progressUpdate.Id,
                "[Progress Update][{@Id}][{@Type}] Progress update value: {@Value}. {@ProgressUpdate}",
                progressUpdate.EpisodeId, progressUpdate.Type, progressUpdate.Value, progressUpdate);
        }
    }
}