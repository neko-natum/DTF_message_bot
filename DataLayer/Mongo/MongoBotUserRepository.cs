using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Threading.Tasks;

namespace DTF_message_bot
{
    internal class MongoBotUserRepository : IBotUserRepository
    {
        private readonly IMongoCollection<User> _users;

        public MongoBotUserRepository(IOptions<MongoOptions> mongoOptionsAccessor)
        {
            _users = new MongoClient(mongoOptionsAccessor.Value.ConnectionString)
                .GetDatabase("messagebot")
                .GetCollection<User>("Users");
        }

        public async Task CreateAsync(User user) => await _users.InsertOneAsync(user);

        public async Task<User> FindByIdAsync(string id) => await _users.Find(x=>x.id == id).SingleOrDefaultAsync();

        public async Task CreateOrReplaceAsync(User user) => await _users.ReplaceOneAsync(x=>x.id == user.id, user, new ReplaceOptions { IsUpsert = true });

        public async Task<long> GetEstimatedCountAsync() => await _users.EstimatedDocumentCountAsync();
    }
}

