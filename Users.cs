using System;
using System.Collections.Generic;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Linq;

namespace DTF_message_bot
{
    class User
    {
        [MongoDB.Bson.Serialization.Attributes.BsonId]
        public string id { get; set; }
        public string username { get; set; }
        public string imagePath { get; set; }
        public double lastMessageTime { get; set; }
        public string lastMessage { get; set; }
        public UserActions lastAction { get; set; }
        public bool isCardExists { get; set; }
        public string Description { get; set; }
        public List<string> links { get; set; }
        public List<string> tags { get; set; }
        public double lastRequestRepost { get; set; }
        public bool isAdmin { get; set; }
        //public double lastRequestHelp { get; set; }

        public void UpdateUser(Channels chan)
        {
            id = chan.id;
            username = chan.lastMessage.author.title;
            imagePath = chan.lastMessage.author.picture;
            lastMessageTime = chan.lastMessage.dtCreated;
            lastMessage = chan.lastMessage.text;
        }

        public void Actions(OsnovaClient worker, IMongoDatabase database)
        {
            UserActions currentAction;
            string answer="";
            var RequestsCollection = database.GetCollection<Request>("Requests");
            switch (lastMessage)
            {
                case string temp when temp.Contains("/help"):
                    currentAction = UserActions.Help;
                    break;
                case string temp when temp.Contains("/repost"):
                    currentAction = UserActions.RequestRepost;
                    break;
                case string temp when temp.Contains("/card"):
                    currentAction = UserActions.RequestAddCard;
                    break;
                case string temp when temp.Contains("/getRequests"):
                    var builder = Builders<Request>.Filter;
                    var filter = builder.Eq("isApproved", false) & builder.Eq("isRejected", false);
                    var result = RequestsCollection.Find(filter).ToList();
                    if (!result.Any())
                    {
                        answer += "Нет ожидающих запросов";
                    }
                    else
                    {
                        int i = 0;
                        foreach (Request request in result)
                        {
                            answer += ++i + ". Пост: " + request.link + " ; дата: " + request.dateCreation + "\n";
                        }
                    }
                    lastAction = currentAction = UserActions.Neutral;
                    break;
                default:
                    switch (lastAction)
                    {
                        case UserActions.RequestRepost:
                            if (!Uri.IsWellFormedUriString(lastMessage, UriKind.RelativeOrAbsolute))
                            {
                                answer += "Не является ссылкой. ";
                                currentAction = UserActions.RequestRepost;
                                break;
                            }
                            if (!lastMessage.Contains("dtf.ru"))
                            {
                                answer += "Не является ссылкой на статью на DTF. ";
                                currentAction = UserActions.RequestRepost;
                                break;
                            }
                            if (worker.isAuthor(id, lastMessage))
                            {
                                RequestsCollection.InsertOneAsync(new Request() 
                                {
                                    id = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds.GetHashCode().ToString(), //не ржать, меня реально плохо с фантазией
                                    user_id = id,
                                    link = lastMessage,
                                    type = "repost",
                                    dateCreation = DateTime.UtcNow
                                }
                                );
                                currentAction = UserActions.TaskCompleted;
                                lastRequestRepost = (double)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                                break;
                            }
                            else
                            {
                                answer += "Принимаются только собственные статьи. ";
                                currentAction = UserActions.RequestRepost;
                                break;
                            }
                        
                        default:
                            currentAction = UserActions.Start;
                            break;
                    }
                    break;
            }
            switch (currentAction)
            {
                case (UserActions.Start):
                    worker.MarkAsRead(id);
                    if (lastAction == UserActions.Undefined)
                        answer += "Добро пожаловать в бота Блогосферы!\n" +
                            "Для работы необходимо ввести одну из команд бота.\n" +
                            "Посмотреть все доступные на данный момент команды можно отправив /help";
                    if (lastAction == UserActions.Start || currentAction == UserActions.Help)
                        answer += "Для работы необходимо ввести одну из команд бота.\n" +
                            "Посмотреть все доступные на данный момент команды можно отправив /help ";
                    lastAction = UserActions.Start;
                    break;
                case (UserActions.Help):
                    worker.MarkAsRead(id);
                    answer += "Текущий список команд:\n" +
                        "/help - вызов справки\n" +
                        "/repost - отправить запрос на репост\n" +
                        "/card - в процессе";
                    if(isAdmin)
                        answer += "\nРасширенный список команд:\n" +
                        "/getRequests - получить список запросов на репост\n" +
                        "/approve %ссылка на пост% - отметить запрос как одобренный\n" +
                        "/reject %ссылка на пост% - отметить запрос как отклонённый";
                    lastAction = UserActions.Help;
                    break;
                case (UserActions.RequestRepost):
                    worker.MarkAsRead(id);
                    if ((double)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds - lastRequestRepost >= /*60480*/0)
                    {
                        lastAction = UserActions.RequestRepost;
                        answer += "Отправьте ссылку на статью для репоста. ";
                    }
                    else
                    {
                        answer += "Не прошло достаточно времени с момента последнего запроса. На данный момент стоит ограничение в 1 запрос в неделю. ";
                        lastAction = UserActions.Start;
                    }
                    break;
                case (UserActions.RequestAddCard):
                    worker.MarkAsRead(id);
                    answer += "На данный момент функционал ещё не готов, у меня лапоньки. ";
                    break;
                case (UserActions.TaskCompleted):
                    worker.MarkAsRead(id);
                    answer += "Данные записаны и отправлены на проверку. ";
                    lastAction = UserActions.Start;
                    break;
                default:
                    worker.MarkAsRead(id);
                    if(lastAction==UserActions.Undefined || lastAction == UserActions.Start || lastAction == UserActions.Help)
                        answer+="Необходимо ввести команду. ";
                    break;
            }
            if(answer!="") worker.AnswerUser(id, answer);
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
        RequestTags = 9,
        TaskCompleted = 10,
        Neutral = 11
    }

    class Request {
        [MongoDB.Bson.Serialization.Attributes.BsonId]
        public string id { get; set; }
        public string user_id { get; set; }
        public string type { get; set; }
        public string link { get; set; }
        public DateTime dateCreation { get; set; }
        public double isApproved { get; set; }
        public double isRejected { get; set; }
    }

}
