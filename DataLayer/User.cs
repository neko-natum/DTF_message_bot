using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace DTF_message_bot
{
    /*
     * класс пользователя
     * пишется в соответствующую таблицу бд
     */
    internal class User
    {
        [BsonId]
        public string id { get; set; } //идентификатор пользователя
        public string username { get; set; } //nuff said
        public string imagePath { get; set; } //nuff said, в основном ради карточки
        public double lastMessageTime { get; set; } //на всякий случай для выгрузки из памяти неактивных, в юникстайме
        public string lastMessage { get; set; } //отсюда берётся всё для дальнейшей логики
        public UserActions lastAction { get; set; } //предыдущее состояние пользователя, очень важно потому что читается только самое последнее сообщение
        public bool isCardExists { get; set; } //заготовка для проверки наличия карточки
        public string Description { get; set; } //описание блога пользователя, возможно получится отказаться, но это сомнительно. Имеется в классе Card
        public List<string> links { get; set; } = new List<string>(); //список ссылок на избранные статьи пользователя, возможно получится отказаться, но это сомнительно. Имеется в классе Card
        public List<string> tags { get; set; } = new List<string>(); //список тегов блога пользователя, возможно получится отказаться, но это сомнительно. Имеется в классе Card
        public double lastRequestRepost { get; set; } //дата в юникстайме последнего запроса на репост чтобы ограничить спам на бота
        public bool isAdmin { get; set; } //понятия не имею зачем, всё равно юзлесс

        public void UpdateUser(string newId, string newUsername, string newImage, double newMessageTime, string newMessage)
        {
            id = newId;
            username = newUsername;
            imagePath = newImage;
            lastMessageTime = newMessageTime;
            lastMessage = newMessage;
        }
    }
}
