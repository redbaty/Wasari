using System.Collections.Generic;

namespace Wasari.Abstractions
{
    public interface ISeasonInfo
    {
        int Season { get; }
        
        string Title { get; }
    }

    public interface ISeasonInfo<T>: ISeasonInfo where T: IEpisodeInfo
    {
        ICollection<T> Episodes { get; }
    }
}