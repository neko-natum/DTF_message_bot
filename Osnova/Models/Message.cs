namespace DTF_message_bot
{
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

}
