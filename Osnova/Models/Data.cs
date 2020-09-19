namespace DTF_message_bot
{
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

}
