using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DTF_message_bot
{
    internal class MongoRepostRequestRepository : IRepostRequestRepository
    {
        private readonly IMongoCollection<Request> _requests;

        public MongoRepostRequestRepository(IOptions<MongoOptions> mongoOptionsAccessor)
        {
            _requests = new MongoClient(mongoOptionsAccessor.Value.ConnectionString)
                .GetDatabase("messagebot")
                .GetCollection<Request>("Requests");
        }

        public async Task CreateAsync(Request request) => await _requests.InsertOneAsync(request);
        public async Task<Request> FindByIdAsync(string id) => await _requests.Find(x => x.id == id).SingleOrDefaultAsync();
        public async Task<List<Request>> FindUnseenAsync() => await _requests.Find(x => !x.isSeen).ToListAsync();
        public async Task MarkAsSeenAsync(string id) => await _requests.UpdateOneAsync(x=>x.id == id, Builders<Request>.Update.Set(x => x.isSeen, true));
    }
}

