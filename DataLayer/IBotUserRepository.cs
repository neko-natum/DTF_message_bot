using System.Threading.Tasks;

namespace DTF_message_bot
{
    internal interface IBotUserRepository
    {
        Task CreateAsync(User user);
        Task CreateOrReplaceAsync(User user);
        Task<User> FindByIdAsync(string id);
        Task<long> GetEstimatedCountAsync();
    }
}

