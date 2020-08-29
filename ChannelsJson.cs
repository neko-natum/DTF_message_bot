using System.Collections.Generic;

namespace DTF_message_bot
{
    public class AnotherPictureData //блядь разработчики очобы вы там совсем уже почему у вас куча разных блоков просто Data?
    {
        public string uuid { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public int size { get; set; }
        public string type { get; set; }
        public string color { get; set; }
        public string hash { get; set; }
        public IList<object> external_service { get; set; }
    }

    public class PictureData
    {
        public string type { get; set; }
        public AnotherPictureData data { get; set; }
    }

    public class Member
    {
        public string id { get; set; }
        public string title { get; set; }
        public string picture { get; set; }
        public PictureData pictureData { get; set; }
        public string lastSeen { get; set; }
        public bool isVerified { get; set; }
        public bool isBanned { get; set; }
    }

    public class Channel
    {
        public string id { get; set; }
        public string idOriginal { get; set; }
        public int type { get; set; }
    }

    public class Author
    {
        public string id { get; set; }
        public string title { get; set; }
        public string picture { get; set; }
        public PictureData pictureData { get; set; }
        public string lastSeen { get; set; }
        public bool isVerified { get; set; }
        public bool isBanned { get; set; }
    }

    public class Message
    {
        public string id { get; set; }
        public int type { get; set; }
        public int status { get; set; }
        public bool removed { get; set; }
        public Channel channel { get; set; }
        public double dtCreated { get; set; }
        public Author author { get; set; }
        public string text { get; set; }
        public object media { get; set; }
        public object replyTo { get; set; }
        public object replyToId { get; set; }
    }
    //кусок для прямых запросов
    public class Channels
    {
        public string id { get; set; }
        public string idOriginal { get; set; }
        public int type { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public string picture { get; set; }
        public PictureData pictureData { get; set; }
        public double dtCreated { get; set; }
        public double dtUpdated { get; set; }
        public object dtLeave { get; set; }
        public bool pendingAcceptance { get; set; }
        public IList<Member> members { get; set; }
        public int role { get; set; }
        public int membersCount { get; set; }
        public int unreadCount { get; set; }
        public bool isMuted { get; set; }
        public bool isEnabledMessenger { get; set; }
        public bool isIgnored { get; set; }
        public int ignoredType { get; set; }
        public bool isBanned { get; set; }
        public bool isVerified { get; set; }
        public Message lastMessage { get; set; }
    }

    public class Result
    {
        public IList<Channels> channels { get; set; }
    }

    public class MessageData
    {
        public string message { get; set; }
        public Result result { get; set; }
    }
    //кусок для сокетов
    public class Data
    {
        public Message message { get; set; }
        public long idTmp { get; set; }
        public string type { get; set; }
        public string action { get; set; }
        public Channel channel { get; set; }
        public string channelId { get; set; }
        public int counter { get; set; }
    }

    public class Socket
    {
        public string channel { get; set; }
        public Data data { get; set; }
    }

}
