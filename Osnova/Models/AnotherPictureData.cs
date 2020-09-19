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

}
