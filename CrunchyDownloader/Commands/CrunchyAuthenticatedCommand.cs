using System;
using System.IO;
using System.Threading.Tasks;
using CliFx.Attributes;
using CrunchyDownloader.App;

namespace CrunchyDownloader.Commands
{
    public class TemporaryCookieFile : IDisposable
    {
        public string Path { get; init; }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (File.Exists(Path))
                File.Delete(Path);
        }
    }

    public abstract class CrunchyAuthenticatedCommand
    {
        protected CrunchyAuthenticatedCommand(CrunchyRollAuthenticationService crunchyRollAuthenticationService)
        {
            CrunchyRollAuthenticationService = crunchyRollAuthenticationService;
        }

        [CommandOption("username", 'u', Description = "Crunchyroll username.", IsRequired = true)]
        public string Username { get; set; }

        [CommandOption("password", 'p', Description = "Crunchyroll password.", IsRequired = true)]
        public string Password { get; set; }

        protected CrunchyRollAuthenticationService CrunchyRollAuthenticationService { get; }

        public async Task<TemporaryCookieFile> CreateCookiesFile()
        {
            var cookies = await CrunchyRollAuthenticationService.GetCookies(Username, Password);
            var cookieFileName = Path.GetTempFileName();
            await File.WriteAllTextAsync(cookieFileName, cookies);
            return new TemporaryCookieFile { Path = cookieFileName };
        }
    }
}