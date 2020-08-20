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
        public void UpdateUser(Channels chan)
        {
            id = chan.id;
            username = chan.lastMessage.author.title;
            imagePath = chan.lastMessage.author.picture;
            lastMessageTime = chan.lastMessage.dtCreated;
            lastMessage = chan.lastMessage.text;
        }

        public void Actions(Network worker)
        {
            UserActions currentAction;
            switch (lastMessage)
            {
                case "/help":
                    currentAction = UserActions.Help;
                    break;
                case "/repost":
                    currentAction = UserActions.RequestRepost;
                    break;
                case "/redact":
                    currentAction = UserActions.RequestHelp;
                    break;
                case "/card":
                    currentAction = UserActions.RequestAddCard;
                    break;
                default:
                    currentAction = UserActions.Start;
                    break;
            }
            switch (currentAction)
            {
                case (UserActions.Start):
                    worker.MarkAsRead(id);
                    if (lastAction == UserActions.Undefined)
                    {
                        worker.AnswerUser(id, "Добро пожаловать! Это автоматический бот.");
                        currentAction = UserActions.Help;
                    }
                    if (lastAction == UserActions.Start || currentAction == UserActions.Help)
                        worker.AnswerUser(id, "Для работы необходимо ввести одну из команд бота.\nПосмотреть все доступные на данный момент команды можно отправив /help ");
                    lastAction = UserActions.Start;
                    break;
                case (UserActions.Help):
                    worker.MarkAsRead(id);
                    worker.AnswerUser(id, "Текущий список команд:\n" +
                        "/help - вызов справки\n" +
                        "/repost - отправить запрос на репост\n" +
                        "/redact - отправить запрос на помощь с доработкой статьи\n" +
                        "/card - в процессе");
                    lastAction = UserActions.Help;
                    break;
                case (UserActions.RequestRepost):
                    worker.MarkAsRead(id);
                    worker.AnswerUser(id, "На данный момент функционал ещё не готов");
                    break;
                case (UserActions.RequestHelp):
                    worker.MarkAsRead(id);
                    worker.AnswerUser(id, "На данный момент функционал ещё не готов");
                    break;
                case (UserActions.RequestAddCard):
                    worker.MarkAsRead(id);
                    worker.AnswerUser(id, "На данный момент функционал ещё не готов");
                    break;
                default:
                    worker.MarkAsRead(id);
                    if(lastAction==UserActions.Undefined || lastAction == UserActions.Start || lastAction == UserActions.Help)
                        worker.AnswerUser(id, "На данный момент функционал ещё не готов");
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
        RequestAddCard = 4,
        RequestAddCard_descr = 5,
        RequestAddCard_links = 6,
        RequestAddCard_tags = 7,
        RequestAddCard_finish = 8,
        RequestTags = 9
    }
}
