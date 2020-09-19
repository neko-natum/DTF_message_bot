namespace DTF_message_bot
{
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

}
