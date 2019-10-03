using AspNetCoreAzureTemplates.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AspNetCoreAzureTemplates.MicrosoftGraph
{
    public class OnBehalfOfMsGraphAuthenticationProvider : IAuthenticationProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AzureAdOptions _authSettings;

        public OnBehalfOfMsGraphAuthenticationProvider(
            IOptions<AzureAdOptions> authenticationOptions,
            IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            _authSettings = authenticationOptions.Value;
        }

        public async Task AuthenticateRequestAsync(HttpRequestMessage request)
        {
            var httpContext = _httpContextAccessor.HttpContext;

            // Get the access token used to call this API
            string token = await httpContext.GetTokenAsync("access_token");

            // We are passing an *assertion* to Azure AD about the current user
            // Here we specify that assertion's type, that is a JWT Bearer token
            string assertionType = "urn:ietf:params:oauth:grant-type:jwt-bearer";

            // User name is needed here only for ADAL, it is not passed to AAD
            // ADAL uses it to find a token in the cache if available
            var user = httpContext.User;
            var claim = user.FindFirst(ClaimTypes.Upn) ?? user.FindFirst(ClaimTypes.Email);
            string userName = claim?.Value;

            var userAssertion = new UserAssertion(token, assertionType, userName);

            var authContext = new AuthenticationContext(_authSettings.Authority);
            var clientCredential = new ClientCredential(_authSettings.ClientId, _authSettings.ClientSecret);

            // Acquire access token
            var result = await authContext.AcquireTokenAsync("https://graph.microsoft.com", clientCredential, userAssertion);

            // Set the authentication header
            request.Headers.Authorization = new AuthenticationHeaderValue(result.AccessTokenType, result.AccessToken);
        }
    }
}
