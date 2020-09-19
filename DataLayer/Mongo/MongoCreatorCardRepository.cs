using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Threading.Tasks;

namespace DTF_message_bot
{
    internal class MongoCreatorCardRepository : ICreatorCardRepository
    {
        private readonly IMongoCollection<Card> _cards;

        public MongoCreatorCardRepository(IOptions<MongoOptions> mongoOptionsAccessor)
        {
            _cards = new MongoClient(mongoOptionsAccessor.Value.ConnectionString)
                .GetDatabase("messagebot")
                .GetCollection<Card>("Cards");
        }

        public async Task CreateAsync(Card card) => await _cards.InsertOneAsync(card);
    }
}

