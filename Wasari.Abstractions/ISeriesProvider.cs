using System.Threading.Tasks;

namespace Wasari.Abstractions
{
    public interface ISeriesProvider<T> where T : ISeasonInfo
    {
        Task<ISeriesInfo<T>> GetSeries(string url);
    }
}