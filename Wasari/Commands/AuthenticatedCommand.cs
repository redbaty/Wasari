using System.IO;
using System.Threading.Tasks;
using CliFx.Attributes;
using Wasari.Crunchyroll.API;
using Wasari.Exceptions;

namespace Wasari.Commands
{
    internal abstract class AuthenticatedCommand
    {
        protected AuthenticatedCommand(CrunchyrollApiServiceFactory crunchyrollApiServiceFactory)
        {
            CrunchyrollApiServiceFactory = crunchyrollApiServiceFactory;
        }

        [CommandOption("username", 'u', Description = "Crunchyroll username.", EnvironmentVariable = "WASARI_USERNAME")]
        public string Username { get; init; }

        [CommandOption("password", 'p', Description = "Crunchyroll password.", EnvironmentVariable = "WASARI_PASSWORD")]
        public string Password { get; init; }
        
        [CommandOption("auth-token", EnvironmentVariable = "WASARI_AUTH_TOKEN")]
        public string AuthenticationToken { get; init; }

        protected CrunchyrollApiServiceFactory CrunchyrollApiServiceFactory { get; }

        protected async Task AuthenticateCrunchyroll()
        {
            if (!string.IsNullOrEmpty(Username) || !string.IsNullOrEmpty(Password))
            {
                if (string.IsNullOrEmpty(Username))
                    throw new CrunchyrollAuthenticationException("Missing username", Username, Password);

                if (string.IsNullOrEmpty(Password))
                    throw new CrunchyrollAuthenticationException("Missing password", Username, Password);

                await CrunchyrollApiServiceFactory.CreateAuthenticatedService(Username, Password);
            }
            else if (!string.IsNullOrEmpty(AuthenticationToken))
            {
                CrunchyrollApiServiceFactory.CreateAuthenticatedFromTokenService(AuthenticationToken);
            }
            else
            {
                await CrunchyrollApiServiceFactory.CreateUnauthenticatedService();
            }
        }
    }
}