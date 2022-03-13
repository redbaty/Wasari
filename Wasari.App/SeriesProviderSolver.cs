using Wasari.Crunchyroll;

namespace Wasari.App;

public class SeriesProviderSolver
{
    public Type GetProvider(Uri url)
    {
        if (url.Host == "beta.crunchyroll.com")
        {
            return typeof(BetaCrunchyrollService);
        }

        throw new NotImplementedException("Failed to determine provider");
    }
}