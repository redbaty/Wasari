using System.IO;
using System.Threading.Tasks;
using CliFx.Attributes;
using CrunchyDownloader.App;
using CrunchyDownloader.Exceptions;
using CrunchyDownloader.Models;

namespace CrunchyDownloader.Commands
{
    public abstract class CrunchyAuthenticatedCommand
    {
        protected CrunchyAuthenticatedCommand(CrunchyRollAuthenticationService crunchyRollAuthenticationService)
        {
            CrunchyRollAuthenticationService = crunchyRollAuthenticationService;
        }

        [CommandOption("username", 'u', Description = "Crunchyroll username.")]
        public string Username { get; init; }

        [CommandOption("password", 'p', Description = "Crunchyroll password.")]
        public string Password { get; init; }

        protected CrunchyRollAuthenticationService CrunchyRollAuthenticationService { get; }

        protected async Task<TemporaryCookieFile> CreateCookiesFile()
        {
            if (string.IsNullOrEmpty(Username) && string.IsNullOrEmpty(Password))
            {
                return null;
            }

            if (string.IsNullOrEmpty(Username))
                throw new CrunchyrollAuthenticationException("Missing username", Username, Password);
            
            if (string.IsNullOrEmpty(Password))
                throw new CrunchyrollAuthenticationException("Missing password", Username, Password);
            
            var cookies = await CrunchyRollAuthenticationService.GetCookies(Username, Password);
            var cookieFileName = Path.GetTempFileName();
            await File.WriteAllTextAsync(cookieFileName, cookies);
            return new TemporaryCookieFile { Path = cookieFileName };
        }
    }
}