using Microsoft.Extensions.Logging;

namespace Wasari.App
{
    public class ProgressUpdate
    {
        public string EpisodeId { get; init; }

        public int Value { get; init; }

        public string Title { get; init; }

        public ProgressUpdateTypes Type { get; init; }

        public EventId Id => new((int)Type, Type.ToString());
    }
}