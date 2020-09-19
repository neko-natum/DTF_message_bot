using MongoDB.Bson.Serialization.Attributes;
using System;

namespace DTF_message_bot
{
    internal class Request
    {
        [BsonId]
        public string id { get; set; }
        public string user_id { get; set; }
        public string type { get; set; }
        public string link { get; set; }
        public DateTime dateCreation { get; set; }
        public bool isSeen { get; set; }
    }
}
