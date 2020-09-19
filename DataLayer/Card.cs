using System.Collections.Generic;

namespace DTF_message_bot
{
    internal class Card
    {
        public string id { get; set; }
        public string username { get; set; }
        public string imagePath { get; set; }
        public string Description { get; set; }
        public List<string> links { get; set; } = new List<string>();
        public List<string> tags { get; set; } = new List<string>();
        public bool isApproved { get; set; }
        public bool isRejected { get; set; }
    }
}
