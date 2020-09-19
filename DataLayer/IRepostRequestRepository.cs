using System.Collections.Generic;
using System.Threading.Tasks;

namespace DTF_message_bot
{
    internal interface IRepostRequestRepository
    {
        Task<List<Request>> FindUnseenAsync();
        Task MarkAsSeenAsync(string id);
        Task<Request> FindByIdAsync(string id);
        Task CreateAsync(Request request);
    }
}

