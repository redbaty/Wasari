using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using PuppeteerExtraSharp;
using PuppeteerExtraSharp.Plugins.ExtraStealth;
using PuppeteerSharp;

namespace Wasari.Crunchyroll;

public class BrowserFactory
{
    public BrowserFactory(IMemoryCache cache)
    {
        Cache = cache;
    }

    private IMemoryCache Cache { get; }

    public static bool Headless { get; set; } = true;
        
    public async Task<Browser> GetBrowserAsync()
    {
        var browser = Cache.Get<Browser>("browser");

        if (browser != null)
            return browser;
            
        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();
        var extra = new PuppeteerExtra();
        extra.Use(new StealthPlugin());

        browser = await extra.LaunchAsync(
            new LaunchOptions
            {
                Headless = Headless,
#if RELEASE
                     Args = new[] {"--no-sandbox"}
#endif
            });

        Cache.Set("browser", browser);
        return browser;
    }

    public Task DisposeAsync()
    {
        var browser = Cache.Get<Browser>("browser");

        if (browser != null)
            return browser.CloseAsync();

        return Task.CompletedTask;
    }
}
