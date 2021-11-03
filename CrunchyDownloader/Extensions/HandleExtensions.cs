using System.Linq;
using System.Threading.Tasks;
using PuppeteerSharp;

namespace CrunchyDownloader.Extensions
{
    internal static class HandleExtensions
    {
        public static async Task<ElementHandle> SingleOrDefaultXPathAsync(this Page page,
            string xpath)
        {
            var handles = await page.XPathAsync(xpath);
            return handles.SingleOrDefault();
        }
        
        public static async Task<ElementHandle> SingleOrDefaultXPathAsync(this ElementHandle elementHandle,
            string xpath)
        {
            var handles = await elementHandle.XPathAsync(xpath);
            return handles.SingleOrDefault();
        }

        public static async Task<T> GetPropertyValue<T>(this JSHandle elementHandle, string property)
        {
            await using var propertyAsync = await elementHandle.GetPropertyAsync(property);

            if (propertyAsync != null)
                return await propertyAsync.JsonValueAsync<T>();

            return default;
        }
    }
}