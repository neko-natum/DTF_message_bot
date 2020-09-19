using System.Threading.Tasks;

namespace DTF_message_bot
{
    internal interface ICreatorCardRepository
    {
        Task CreateAsync(Card card);
    }
}

