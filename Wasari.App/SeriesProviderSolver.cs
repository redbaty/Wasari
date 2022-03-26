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

        if (url.Host is "crunchyroll.com" or "www.crunchyroll.com")
        {
            return typeof(CrunchyrollService);
        }

        throw new NotImplementedException("Failed to determine provider");
    }
}