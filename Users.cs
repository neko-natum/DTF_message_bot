using System;
using System.Collections.Generic;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Linq;

namespace DTF_message_bot
{
    /*
     * класс пользователя
     * пишется в соответствующую таблицу бд
     */
    class User
    {
        [MongoDB.Bson.Serialization.Attributes.BsonId]
        public string id { get; set; } //идентификатор пользователя
        public string username { get; set; } //nuff said
        public string imagePath { get; set; } //nuff said, в основном ради карточки
        public double lastMessageTime { get; set; } //на всякий случай для выгрузки из памяти неактивных, в юникстайме
        public string lastMessage { get; set; } //отсюда берётся всё для дальнейшей логики
        public UserActions lastAction { get; set; } //предыдущее состояние пользователя, очень важно потому что читается только самое последнее сообщение
        public bool isCardExists { get; set; } //заготовка для проверки наличия карточки
        public string Description { get; set; } //описание блога пользователя, возможно получится отказаться, но это сомнительно. Имеется в классе Card
        public List<string> links { get; set; } //список ссылок на избранные статьи пользователя, возможно получится отказаться, но это сомнительно. Имеется в классе Card
        public List<string> tags { get; set; } //список тегов блога пользователя, возможно получится отказаться, но это сомнительно. Имеется в классе Card
        public double lastRequestRepost { get; set; } //дата в юникстайме последнего запроса на репост чтобы ограничить спам на бота
        public bool isAdmin { get; set; } //понятия не имею зачем, всё равно юзлесс
        //public double lastRequestHelp { get; set; }

        public User()
        {
            links = new List<string>();
            tags = new List<string>();
        }
        /*
         * не помню зачем, но на всякий обновляет данные для новых абонентов
         */
        public void UpdateUser(string newId, string newUsername, string newImage, double newMessageTime, string newMessage)
        {
            id = newId;
            username = newUsername;
            imagePath = newImage;
            lastMessageTime = newMessageTime;
            lastMessage = newMessage;
        }
        /*
         * обновление конкретных полей в таблице пользователей
         * данные сюда передавать через фильтр update
         */
        private void UpdateUserField(IMongoCollection<User> UsersCollection, UpdateDefinition<User> update)
        {
            var filter = Builders<User>.Filter.Eq("id", id);
            UsersCollection.UpdateOne(filter, update);
        }
        /* 
        * основная функция логики бота, возвращает ответное сообщение в зависимости от входящего
        * по возможности менять только её чтобы не сломать что-то в процессе
        */
        public string Actions(OsnovaClient worker, IMongoDatabase database) 
        {
            UserActions currentAction = UserActions.Undefined;
            string answer="";
            var RequestsCollection = database.GetCollection<Request>("Requests");
            var builder = Builders<Request>.Filter;
            var CardsCollection = database.GetCollection<Card>("Cards");
            var updateBuilder = Builders<User>.Update;
            switch (lastMessage) //проверка последнего непрочитанного
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
                    if (isAdmin)
                    {
                        var filter = builder.Eq("isApproved", false);
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
                    }
                    lastAction = currentAction = UserActions.Neutral;
                    break;
                case string temp when temp.Contains("/end"):
                    if (lastAction == UserActions.RequestAddCard_links)
                        currentAction = UserActions.RequestAddCard_tags;
                    else if (lastAction == UserActions.RequestAddCard_tags)
                        currentAction = UserActions.RequestAddCard_finish;
                    else
                    {
                        answer += "Нечего завершать";
                        currentAction = UserActions.Neutral;
                    }
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
                                    id = worker.GetArticleID(lastMessage),
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
                        case UserActions.RequestAddCard:
                            if (lastMessage.Length > 1000)
                            {
                                answer += "Превышен порог по знакам.\n";
                                currentAction = UserActions.RequestAddCard;
                            }
                            else
                            {
                                Description = lastMessage;
                                currentAction = UserActions.RequestAddCard_links;
                            }
                            break;
                        case UserActions.RequestAddCard_links:
                            if (worker.isAuthor(id, lastMessage))
                            {
                                links.Add(lastMessage);
                                if (links.Count < 5)
                                    currentAction = UserActions.Neutral;
                                if (links.Count == 5)
                                    currentAction = UserActions.RequestAddCard_tags;
                                break;
                            }
                            else
                            {
                                answer += "Принимаются только собственные статьи. ";
                                currentAction = UserActions.Neutral;
                                break;
                            }
                        case UserActions.RequestAddCard_tags:
                            tags.Add(lastMessage);
                            if (tags.Count < 5)
                                currentAction = UserActions.Neutral;
                            if (tags.Count == 5)
                                currentAction = UserActions.RequestAddCard_finish;
                            break;
                        default:
                            currentAction = UserActions.Start;
                            break;
                    }
                    break;
            }
            switch (currentAction) //что делаем в зависимости от последнего сообщения
            {
                case (UserActions.Start):
                    if (lastAction == UserActions.Undefined)
                        answer += "Добро пожаловать в бота Блогосферы!\n" +
                            "Для работы необходимо ввести одну из команд бота.\n" +
                            "Посмотреть все доступные на данный момент команды можно отправив /help";
                    if (lastAction == UserActions.Start || lastAction == UserActions.TaskCompleted)
                        answer += "Для работы необходимо ввести одну из команд бота.\n" +
                            "Посмотреть все доступные на данный момент команды можно отправив /help ";
                    lastAction = UserActions.Start;
                    break;
                case (UserActions.Help):
                    answer += "Текущий список команд:\n" +
                        "/help - вызов справки\n" +
                        "/repost - отправить запрос на репост\n"/* +
                        "/card - в процессе"*/;
                    if(isAdmin)
                        answer += "\nРасширенный список команд:\n" +
                        "/getRequests - получить список запросов на репост\n"/* +
                        "/approve %ссылка на пост% - отметить запрос как одобренный\n" +
                        "/reject %ссылка на пост% - отметить запрос как отклонённый"*/;
                    lastAction = UserActions.Help;
                    break;
                case (UserActions.RequestRepost):
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
                    answer += "Введите описание вашего блога. Постарайтесь ограничиться 1000 символов.";
                    lastAction = UserActions.RequestAddCard;
                    break;
                case (UserActions.TaskCompleted):
                    answer += "Данные записаны и отправлены на проверку. ";
                    lastAction = UserActions.Start;
                    break;
                case (UserActions.RequestAddCard_links):
                    answer += "Отправьте по одной за сообщение ссылке на лучшие по вашему мнению посты вашего авторства, но не более 5.\n" +
                        "Чтобы завершить заполнение списка ссылок введите /end";
                    lastAction = UserActions.RequestAddCard_links;
                    break;
                case (UserActions.RequestAddCard_tags):
                    answer += "Отправьте по одной за сообщение теги, которые вы чаще всего используете, но не более 5. Старайтесь использовать те теги, которые используются в общих подсайтах.\n" +
                        "Чтобы завершить заполнение списка тегов введите /end";
                    lastAction = UserActions.RequestAddCard_tags;
                    break;
                case (UserActions.RequestAddCard_finish):
                    answer += "Создание карточки завершено, ожидайте проверки";
                    CardsCollection.InsertOneAsync(new Card()
                    {
                        id = id,
                        username = username,
                        imagePath = imagePath,
                        Description = Description,
                        links = links,
                        tags = tags
                    });
                    UpdateUserField(database.GetCollection<User>("Users"), updateBuilder.Set("Description", Description).Set("links", links).Set("tags", tags));
                    lastAction = UserActions.TaskCompleted;
                    break;
                default:
                    if(lastAction==UserActions.Undefined || lastAction == UserActions.Start || lastAction == UserActions.Help)
                        answer+="Необходимо ввести команду. ";
                    /*if (lastAction == UserActions.RequestAddCard_links)
                    {
                        if(links.Count==0)
                    }*/
                    break;
            }
            var update = updateBuilder.Set("lastMessageTime", lastMessageTime).Set("lastMessage", lastMessage).Set("lastAction", lastAction);
            UpdateUserField(database.GetCollection<User>("Users"), update);
            return answer;
        }
    }
    /*
     * коллекция для состояний абонента
     * чисто ради визуального удобства
     */
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
    }
    class Card
    {
        public string id { get; set; }
        public string username { get; set; }
        public string imagePath { get; set; }
        public string Description { get; set; }
        public List<string> links { get; set; }
        public List<string> tags { get; set; }
        public bool isApproved { get; set; }
        public bool isRejected { get; set; }

        public Card()
        {
            links = new List<string>();
            tags = new List<string>();
        }
    }
}
