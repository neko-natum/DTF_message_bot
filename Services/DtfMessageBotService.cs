using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DTF_message_bot
{
    internal class DtfMessageBotService : ContinuousHostedService
    {
        private readonly OsnovaClient _osnova;
        private readonly IBotUserRepository _users;
        private readonly IRepostRequestRepository _requests;
        private readonly ICreatorCardRepository _cards;
        private readonly HealthchecksOptions _hcOptions;
        private readonly ILogger<DtfMessageBotService> _logger;

        public DtfMessageBotService(
            OsnovaClient osnova, 
            IBotUserRepository users,
            IRepostRequestRepository requests,
            ICreatorCardRepository cards,
            IOptions<HealthchecksOptions> hcOptionsAccessor,
            ILogger<DtfMessageBotService> logger,
            IHostApplicationLifetime host,
            ILogger<ContinuousHostedService> baseLogger) : base(host, baseLogger)
        {
            _osnova = osnova;
            _users = users;
            _requests = requests;
            _cards = cards;
            _hcOptions = hcOptionsAccessor.Value;
            _logger = logger;
        }

        private async Task<string> HandleCurrentUserState(User user)
        {
            var currentAction = UserActions.Undefined;
            var answer = string.Empty;
            switch (user.lastMessage) //проверка последнего непрочитанного
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
                    if (user.isAdmin)
                    {
                        var result = await _requests.FindUnseenAsync();
                        if (!result.Any())
                        {
                            answer += "Нет ожидающих запросов";
                        }
                        else
                        {
                            answer += string.Join('\n', result.Select((request, i) => $"{i + 1}. Пост: {request.link} ; дата: {request.dateCreation}")) + '\n';
                        }
                    }
                    user.lastAction = currentAction = UserActions.Neutral;
                    break;
                case string temp when temp.Contains("/markSeen"):
                    if (user.isAdmin)
                    {
                        var requestId = temp.Split(" ")[1];
                        await _requests.MarkAsSeenAsync(requestId);
                        answer += "Отметил если было что отмечать";
                    }
                    user.lastAction = UserActions.Neutral;
                    break;
                case string temp when temp.Contains("/end"):
                    if (user.lastAction == UserActions.RequestAddCard_links)
                    {
                        currentAction = UserActions.RequestAddCard_tags;
                    }
                    else if (user.lastAction == UserActions.RequestAddCard_tags)
                    {
                        currentAction = UserActions.RequestAddCard_finish;
                    }
                    else if (user.lastAction == UserActions.RequestRepost)
                    {
                        currentAction = UserActions.Neutral;
                        answer += "Операция отменена";
                    }
                    else
                    {
                        answer += "Нечего завершать";
                        currentAction = UserActions.Neutral;
                    }
                    break;
                default:
                    switch (user.lastAction)
                    {
                        case UserActions.RequestRepost:
                            if (!Uri.IsWellFormedUriString(user.lastMessage, UriKind.RelativeOrAbsolute))
                            {
                                answer += "Не является ссылкой. ";
                                currentAction = UserActions.RequestRepost;
                                break;
                            }
                            if (!user.lastMessage.Contains("dtf.ru"))
                            {
                                answer += "Не является ссылкой на статью на DTF. ";
                                currentAction = UserActions.RequestRepost;
                                break;
                            }
                            if (await _osnova.isAuthor(user.id, user.lastMessage))
                            {
                                var articleId = await _osnova.GetArticleID(user.lastMessage);
                                if ((await _requests.FindByIdAsync(articleId)) != null)
                                {
                                    answer += "Вы уже отправляли эту ссылку. ";
                                    currentAction = UserActions.RequestRepost;
                                }
                                else
                                {
                                    await _requests.CreateAsync(new Request()
                                    {
                                        id = await _osnova.GetArticleID(user.lastMessage),
                                        user_id = user.id,
                                        link = user.lastMessage,
                                        type = "repost",
                                        dateCreation = DateTime.UtcNow
                                    });
                                    currentAction = UserActions.TaskCompleted;
                                    await _osnova.AnswerUser(_osnova.possessionHash != null ? _osnova.possessionID : _osnova.ID, "Новый входящий реквест: " + user.lastMessage + "\n");
                                    user.lastRequestRepost = (double) (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                                }
                                break;
                            }
                            else
                            {
                                answer += "Принимаются только собственные статьи. ";
                                currentAction = UserActions.RequestRepost;
                                break;
                            }
                        case UserActions.RequestAddCard:
                            if (user.lastMessage.Length > 1000)
                            {
                                answer += "Превышен порог по знакам.\n";
                                currentAction = UserActions.RequestAddCard;
                            }
                            else
                            {
                                user.Description = user.lastMessage;
                                currentAction = UserActions.RequestAddCard_links;
                            }
                            break;
                        case UserActions.RequestAddCard_links:
                            if (await _osnova.isAuthor(user.id, user.lastMessage))
                            {
                                user.links.Add(user.lastMessage);
                                if (user.links.Count < 5)
                                {
                                    currentAction = UserActions.Neutral;
                                }

                                if (user.links.Count == 5)
                                {
                                    currentAction = UserActions.RequestAddCard_tags;
                                }

                                break;
                            }
                            else
                            {
                                answer += "Принимаются только собственные статьи. ";
                                currentAction = UserActions.Neutral;
                                break;
                            }
                        case UserActions.RequestAddCard_tags:
                            user.tags.Add(user.lastMessage);
                            if (user.tags.Count < 5)
                            {
                                currentAction = UserActions.Neutral;
                            }

                            if (user.tags.Count == 5)
                            {
                                currentAction = UserActions.RequestAddCard_finish;
                            }

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
                    if (user.lastAction == UserActions.Undefined)
                    {
                        answer += "Добро пожаловать в бота Блогосферы!\n" +
                            "Для работы необходимо ввести одну из команд бота.\n" +
                            "Посмотреть все доступные на данный момент команды можно отправив /help";
                    }

                    /*if (lastAction == UserActions.Start || lastAction == UserActions.TaskCompleted)
                    {
                        answer += "Для работы необходимо ввести одну из команд бота.\n" +
                            "Посмотреть все доступные на данный момент команды можно отправив /help ";
                    }*/

                    user.lastAction = UserActions.Start;
                    break;
                case (UserActions.Help):
                    answer += "Текущий список команд:\n" +
                        "/help - вызов справки\n" +
                        "/repost - отправить запрос на репост"/* +
                        "/card - в процессе"*/;
                    if (user.isAdmin)
                    {
                        answer += "\nРасширенный список команд:\n" +
                        "/getRequests - получить список запросов на репост\n" +
                        "/markSeen %id поста% - отметить запрос как просмотренный"/* +
                        "/reject %ссылка на пост% - отметить запрос как отклонённый"*/;
                    }

                    user.lastAction = UserActions.Help;
                    break;
                case (UserActions.RequestRepost):
                    if ((double) (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds - user.lastRequestRepost >= _osnova.RepostTimeout)
                    {
                        user.lastAction = UserActions.RequestRepost;
                        answer += "Отправьте ссылку на статью для репоста. Для отмены команды отправьте /end";
                    }
                    else
                    {
                        answer += "Не прошло достаточно времени с момента последнего запроса. На данный момент стоит ограничение в 1 запрос в день. ";
                        user.lastAction = UserActions.Start;
                    }
                    break;
                case (UserActions.RequestAddCard):
                    answer += "Введите описание вашего блога. Постарайтесь ограничиться 1000 символов.";
                    user.lastAction = UserActions.RequestAddCard;
                    break;
                case (UserActions.TaskCompleted):
                    answer += "Данные записаны и отправлены на проверку. ";
                    user.lastAction = UserActions.Start;
                    break;
                case (UserActions.RequestAddCard_links):
                    answer += "Отправьте по одной за сообщение ссылке на лучшие по вашему мнению посты вашего авторства, но не более 5.\n" +
                        "Чтобы завершить заполнение списка ссылок введите /end";
                    user.lastAction = UserActions.RequestAddCard_links;
                    break;
                case (UserActions.RequestAddCard_tags):
                    answer += "Отправьте по одной за сообщение теги, которые вы чаще всего используете, но не более 5. Старайтесь использовать те теги, которые используются в общих подсайтах.\n" +
                        "Чтобы завершить заполнение списка тегов введите /end";
                    user.lastAction = UserActions.RequestAddCard_tags;
                    break;
                case (UserActions.RequestAddCard_finish):
                    answer += "Создание карточки завершено, ожидайте проверки";
                    await _cards.CreateAsync(new Card()
                    {
                        id = user.id,
                        username = user.username,
                        imagePath = user.imagePath,
                        Description = user.Description,
                        links = user.links,
                        tags = user.tags
                    });
                    user.lastAction = UserActions.TaskCompleted;
                    break;
                default:
                    if (user.lastAction == UserActions.Undefined || user.lastAction == UserActions.Start || user.lastAction == UserActions.Help)
                    {
                        answer += "Необходимо ввести команду. ";
                    }
                    break;
            }
            return answer;
        }

#error TODO: create separate message reading service from osnovaclient

        /*
         * Рабочий цикл бота
         * Здесь инициируется прослушка сокетов и, если сокеты как обычно лежат, спам запросами
         * а также вызов логики самого бота
         */
        protected override async Task RunServiceAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Bot for Osnova-based messenger\nStarted up!");
            var firstRun = true; //первый прогон после запуска всегда прямым запросом чтобы отследить входящие до включения

            _logger.LogDebug("Connecting to MongoDB...");
            await Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (ex, timeSpan) => _logger.LogError(ex, $"MongoDB connection failed, waiting {timeSpan.TotalSeconds}s and retrying...")
                )
                .ExecuteAsync(async () => _logger.LogInformation("Known users: {0}", await _users.GetEstimatedCountAsync()));
            _logger.LogDebug("MongoDB connection success.");

            await _osnova.StartAsync();
            var activeUsers = new List<User>();
            do
            {
                if (_osnova.isConnected && firstRun == false) //работа на сокетах
                {
                    if (_osnova.socketTasks.TryDequeue(out var queuedUser))
                    {
                        await HandleMessageAsync(queuedUser.id, queuedUser.username, queuedUser.imagePath, queuedUser.lastMessageTime, queuedUser.lastMessage);
                    }
                }
                else //работа на прямых запросах
                {
                    await _osnova.Listen();
                    if (_osnova.LastStatus > 0)
                    {
                        var data = await _osnova.RequestChannelsData();
                        foreach (var chan in data.result.channels)
                        {
                            if (chan.unreadCount != 0)
                            {
                                await HandleMessageAsync(chan.id, chan.lastMessage.author.title, chan.lastMessage.author.picture, chan.lastMessage.dtCreated, chan.lastMessage.text);
                            }
                        }
                    }
                    else if (_osnova.LastStatus == 0)
                    {
                        await Task.Delay(1000);
                    }
                    else if (_osnova.LastStatus == -1)
                    {
                        _logger.LogError("Network error. Shutdown.");
                    }
                    firstRun = false;
                }

                if (_osnova.isError)
                {
                    _logger.LogError("Error in sockets");
                    _osnova.isError = false;
                }

                if(_osnova.mHashLifetime < (double)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds)
                {
                    _logger.LogInformation("Sockets should be restarted right now");
                    _osnova.isConnected = false;
                    await _osnova.RestartSocket();
                    firstRun = true;
                }
            }
            while (!cancellationToken.IsCancellationRequested && _osnova.LastStatus >= 0);
        }

        private async Task HandleMessageAsync(string id, string username, string imagePath, double lastMessageTime, string lastMessage)
        {
            if (lastMessage == _hcOptions.HealthcheckMessage)
            {
                if (_hcOptions.HealthchecksEnabled ?? false)
                {
                    using var httpClient = new HttpClient();
                    await httpClient.GetAsync(_hcOptions.HealthcheckUri);
                }
                return;
            }

            var currentUser = await _users.FindByIdAsync(id);
            if (currentUser is null)
            {
                currentUser = new User
                {
                    id = id,
                    username = username,
                    imagePath = imagePath,
                    lastMessageTime = lastMessageTime,
                    lastMessage = lastMessage,
                    lastAction = UserActions.Undefined,
                    isCardExists = false,
                    isAdmin = false
                };
                _logger.LogInformation("New user! ID: {0}", currentUser.id);
            }
            else
            {
                currentUser.UpdateUser(id, username, imagePath, lastMessageTime, lastMessage);
            }

            _logger.LogInformation("Message from user {0}: {1}", currentUser.id, currentUser.lastMessage);

            var answer = await HandleCurrentUserState(currentUser);
            if (answer != string.Empty)
            {
                await _osnova.AnswerUser(id, answer);
            }
            await _users.CreateOrReplaceAsync(currentUser);
            await _osnova.MarkAsRead(id);
        }
    }
}

