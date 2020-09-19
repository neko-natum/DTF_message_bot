using System.Collections.Generic;

namespace DTF_message_bot
{
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

}
