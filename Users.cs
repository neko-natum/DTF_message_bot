using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace DTF_message_bot
{
    class User
    {
        public string id { get; set; }
        public string username { get; set; }
        public string imagePath { get; set; }
        public double lastMessageTime { get; set; }
        public string lastMessage { get; set; }
        public UserActions lastAction { get; set; }
        public bool isCardExists { get; set; }
        public string Description { get; set; }
        public ArrayList links { get; set; }
        public ArrayList tags { get; set; }
        public DateTime lastRequestRepost { get; set; }
        public DateTime lastRequestHelp { get; set; }

        public void SaveUserJson() //запись в файл данных о пользователе
        {
            File.WriteAllText("users/" + id + ".json", JsonSerializer.Serialize<User>(this));
        }
        public void Actions(Network worker)
        {
            switch ((int)lastAction)
            {
                case -1:
                    worker.MarkAsRead(id);
                    worker.AnswerUser(id,"Бот работает, помощи ещё нет, но скоро будет");
                    lastAction = UserActions.Start;
                    break;
                case 0:
                    worker.AnswerUser(id, "Не пытайся что-то изменитб");
                    lastAction = UserActions.Start;
                    goto default;
                default:
                    worker.MarkAsRead(id);
                    break;
            }
        }
    }

    public enum UserActions
    {
        Undefined = -1,
        Start = 0,
        Help = 1,
        RequestRepost = 2,
        RequestHelp = 3,
        RequestAddCart = 4,
        RequestAddCart_descr = 5,
        RequestAddCart_links = 6,
        RequestAddCart_tags = 7,
        RequestAddCart_finish = 8,
        RequestTags = 9
    }
}
