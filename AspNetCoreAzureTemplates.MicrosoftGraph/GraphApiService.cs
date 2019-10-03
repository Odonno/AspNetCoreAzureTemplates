using Microsoft.Graph;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AspNetCoreAzureTemplates.MicrosoftGraph
{
    public interface IGraphApiService
    {
        Task<User> GetCurrentProfileAsync();
        Task<IEnumerable<User>> SearchUsersAsync(string search, int limit);
        Task<IEnumerable<User>> GetUsersAsync(IEnumerable<string> userIds);
    }

    public class GraphApiService : IGraphApiService
    {
        private readonly IGraphServiceClient _client;

        public GraphApiService(IGraphServiceClient client)
        {
            _client = client;
        }

        public Task<User> GetCurrentProfileAsync()
        {
            return _client.Me.Request().GetAsync();
        }

        public async Task<IEnumerable<User>> SearchUsersAsync(string search, int limit)
        {
            var users = new List<User>();

            var currentReferencesPage = await _client.Users
                .Request()
                .Top(limit)
                .Filter($"startsWith(displayName, '{search}') or startswith(mail, '{search}')")
                .GetAsync();

            users.AddRange(currentReferencesPage);

            return users;
        }

        public async Task<IEnumerable<User>> GetUsersAsync(IEnumerable<string> userIds)
        {
            var users = new List<User>();

            var currentReferencesPage = await _client.Users
                .Request()
                .Filter(
                    string.Join(" or ", userIds.Select(id => $"id eq '{id}'"))
                )
                .GetAsync();

            users.AddRange(currentReferencesPage);

            return users;
        }

        // TODO : Add more functions if necessary...
    }
}
