using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace AspNetCoreAzureTemplates.Identity
{
    public interface IIdentityService
    {
        IEnumerable<string> GetRoles();
    }

    public class AzureAdIdentityService : IIdentityService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AzureAdIdentityService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public IEnumerable<string> GetRoles()
        {
            return _httpContextAccessor.HttpContext.User.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value);
        }
    }
}
