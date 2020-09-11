using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SocketIOClient;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DTF_message_bot
{
    /*
     * Реализация запросов по API Очобы
     * Все запросы GET/POST так или иначе добавлять сюда
     */
    internal class OsnovaClient
    {
        private string site;
        private readonly HttpClient clientApi;
        private readonly HttpClient clientRaw;
        private SocketIO clientSocket;
        public int LastStatus;
        public string LastResult;
        public string ID;
        public string possessionID;
        public string possessionHash;
        public string mHash;
        public int mHashLifetime;
        public int RepostTimeout;
        public bool isConnected;
        public bool isError;
        public ConcurrentQueue<User> socketTasks;

        public OsnovaClient(IOptions<OsnovaOptions> optionsAccessor)
        {
            var options = optionsAccessor.Value;
            site = options.Host;
            socketTasks = new ConcurrentQueue<User>();
            clientApi = new HttpClient();
            clientRaw = new HttpClient();
            clientApi.DefaultRequestHeaders.Add("X-Device-Token", options.Token);
            clientRaw.DefaultRequestHeaders.Add("Cookie", "osnova-remember=" + options.osnova_remember + "; osnova-aid=" + options.osnova_aid + "; osnova-possession=" + options.osnova_possession);
            clientRaw.DefaultRequestHeaders.Add("user-agent", "Mozilla/5444.0");
            clientRaw.DefaultRequestHeaders.Add("x-this-is-csrf", "THIS IS SPARTA!");
            ID = options.SelfID;
            possessionID = options.PossessionID;
            clientApi.BaseAddress = new Uri("https://api." + site + ".ru/" + options.Version + "/");
            if (possessionID != "")
            {
                possessionHash = Possession().ConfigureAwait(false).GetAwaiter().GetResult();
                if (possessionHash != null)
                {
                    clientApi.DefaultRequestHeaders.Add("X-Device-Possession-Token", possessionHash);
                }
            }
            UpdateMHash().ConfigureAwait(false).GetAwaiter().GetResult();
            RepostTimeout = options.RepostTimeout;

            //clientSocket = new SocketIO("wss://ws-sio.dtf.ru/socket.io/?EIO=3&transport=websocket");
            //await StartAsync();
        }

        public async Task StartAsync() //слушатель сокетов
        {
            clientSocket = new SocketIO("wss://ws-sio."+site+".ru/?EIO=3&transport=websocket");
            clientSocket.OnConnected += _socketIoClient_OnConnected;
            clientSocket.OnDisconnected += (_, e) => isConnected = false;
            clientSocket.OnError += (_, e) => isError = true;
            clientSocket.OnReconnecting += (_, e) => UpdateMHash().ConfigureAwait(false).GetAwaiter().GetResult();
            //clientSocket.OnPing += (_, e) => _logger.LogInformation("Ping");
            //clientSocket.OnPong += (_, e) => _logger.LogInformation($"Pong in {(int)e.TotalMilliseconds}ms");
            clientSocket.On("event", response =>
            {
                var data = response.GetValue<dynamic>();
                if (((string) data.channel == "m:" + mHash) && ((string) data.data.action == "addMessage"))
                {
                    if ((string) data.data.message.author.id != ID && (string) data.data.message.author.id != possessionID)
                    {
                        socketTasks.Enqueue(new User
                        {
                            id = (string) data.data.message.author.id,
                            username = (string) data.data.message.author.title,
                            imagePath = (string) data.data.message.author.picture,
                            lastMessageTime = (double) data.data.message.dtCreated,
                            lastMessage = (string) data.data.message.text
                        });
                    }
                }
            });
            await clientSocket.ConnectAsync();
        }

        private async void _socketIoClient_OnConnected(object sender, EventArgs e) //подключение к сокетам
        {
            isConnected = true;
            await clientSocket.EmitAsync("subscribe", new { channel = "m:" + mHash });
        }

        public async Task StopAsync() //остановка слушателя сокетов
        {
            if (clientSocket != null)
            {
                await clientSocket.DisconnectAsync();
            }
        }

        private static async Task<string> RawGET(HttpClient client, string query) //Отправка GET-запроса с полученим чистого json
        {
            try
            {
                return await client.GetStringAsync(query);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return "error";
            }
        }

        private static async Task<string> RawPOST(HttpClient client, string query, MultipartFormDataContent data) //Отправка POST-запроса с полученим чистого json
        {
            try
            {
                var response = await client.PostAsync(query, data);
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return "error";
            }
        }
        private static async Task<string> PossessionPost(HttpClient client, string query, MultipartFormDataContent data) //Отправка POST-запроса с получением хэша possession
        {
            try
            {
                var response = await client.PostAsync(query, data);
                return response.Headers.GetValues("x-device-possession-token").First().ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return "error";
            }
        }

        private async Task<string> Possession() //чтобы писать от имени подсайта
        {
            var request = JsonConvert.DeserializeObject<dynamic>(RawGET(clientApi, "locate?url=" + possessionID).Result);
            var requestParameters = new[]
            {
                new KeyValuePair<string,string>("id", (string)request.result.data.id)
            };
            var content = new MultipartFormDataContent();
            foreach (var keyValuePair in requestParameters)
            {
                content.Add(new StringContent(keyValuePair.Value),
                    string.Format("\"{0}\"", keyValuePair.Key));
            }
            possessionID = (string) request.result.data.id;
            return await PossessionPost(clientApi, "auth/possess", content);
        }

        private async Task UpdateMHash() //потрясающая работа с сокетами мессенджера
        {
            var hashQuery = JsonConvert.DeserializeObject<dynamic>(await RawGET(clientRaw, "https://" + site + ".ru/u/" + ID + "/stats?mode=ajax"))["module.auth"];
            mHash = hashQuery.m_hash;
            mHashLifetime = hashQuery.m_hash_expiration_time;
        }

        public async Task Listen() //"Слушает" входящие через запрос
        {
            LastResult = await RawGET(clientApi, "m/counter");
            if (LastResult == "error")
            {
                LastStatus = -1;
            }

            int.TryParse(string.Join("", LastResult.Where(c => char.IsDigit(c))), out LastStatus);
        }

        public async Task AnswerUser(string chanId, string answer) //Отправляет сообщение в указанный канал
        {
            var requestParameters = new[] //отправляем послание
            {
                new KeyValuePair<string,string>("channelId", chanId),
                new KeyValuePair<string,string>("text", answer),
                new KeyValuePair<string,string>("ts", "1"),
                new KeyValuePair<string,string>("idTmp", "1"),
                new KeyValuePair<string,string>("media", "[]")
            };
            var content = new MultipartFormDataContent();
            foreach (var keyValuePair in requestParameters)
            {
                content.Add(new StringContent(keyValuePair.Value),
                    string.Format("\"{0}\"", keyValuePair.Key));
            }
            LastResult = await RawPOST(clientApi, "m/send", content);
            await MarkAsRead(chanId);
        }

        public async Task MarkAsRead(string chanId) //Отмечает все сообщения как прочитанные
        {
            var requestParameters = new[]
            {
                new KeyValuePair<string,string>("channelId", chanId),
                new KeyValuePair<string,string>("beforeTime", "0")
            };
            var content = new MultipartFormDataContent();
            foreach (var keyValuePair in requestParameters)
            {
                content.Add(new StringContent(keyValuePair.Value),
                    string.Format("\"{0}\"", keyValuePair.Key));
            }
            LastResult = await RawPOST(clientApi, "m/markAsRead", content);
        }

        public async Task<bool> isAuthor(string id, string link) //проверка авторства
        {
            var responseStr = await RawGET(clientApi, "locate?url=" + link);
            return responseStr.Contains("\"author\":{\"id\":" + id + ",");
        }

        public async Task<string> GetArticleID(string link) //получение идентификатора статьи
        {
            var request = JsonConvert.DeserializeObject<dynamic>(await RawGET(clientApi, "locate?url=" + link));
            return (string) request.result.data.id;
        }

        public async Task<MessageData> RequestChannelsData() //Запрашивает информацию о входящих
=> JsonConvert.DeserializeObject<MessageData>(await RawGET(clientApi, "m/channels"));
    }
}
