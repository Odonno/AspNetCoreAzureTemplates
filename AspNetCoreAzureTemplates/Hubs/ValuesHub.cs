using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace AspNetCoreAzureTemplates.Hubs
{
    [Authorize]
    public class ValuesHub : Hub
    {
        private int _currentValue = 0;

        [Authorize(Policy = "RequireReaderRole")]
        public Task Get()
        {
            return Clients.Caller.SendAsync("Send", _currentValue); // TODO : Send existing data
        }

        [Authorize(Policy = "RequireWriterRole")]
        public Task Publish(int value)
        {
            _currentValue = value;
            return Clients.Others.SendAsync("Send", _currentValue); // TODO : Save data
        }
    }
}
