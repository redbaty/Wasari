using System.Collections.Generic;

namespace Wasari.Abstractions
{
    public interface ISeriesInfo
    {
        string Name { get; }
    }

    public interface ISeriesInfo<T> : ISeriesInfo where T : ISeasonInfo
    {
        ICollection<T> Seasons { get; }
    }
}