using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SocketIOClient;
using System.Threading;
using System.Text;
using System.Collections.Concurrent;

namespace DTF_message_bot
{
    /*
     * Реализация запросов по API Очобы
     * Все запросы GET/POST так или иначе добавлять сюда
     */
    class OsnovaClient
    {
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
            socketTasks = new ConcurrentQueue<User>();
            clientApi = new HttpClient();
            clientRaw = new HttpClient();
            clientApi.DefaultRequestHeaders.Add("X-Device-Token", options.Token);
            clientRaw.DefaultRequestHeaders.Add("Cookie", "osnova-remember="+options.osnova_remember+"; osnova-aid="+options.osnova_aid+ "; osnova-possession=" + options.osnova_possession);
            clientRaw.DefaultRequestHeaders.Add("user-agent", "Mozilla/5444.0");
            clientRaw.DefaultRequestHeaders.Add("x-this-is-csrf", "THIS IS SPARTA!");
            ID = options.SelfID;
            possessionID = options.PossessionID;
            clientApi.BaseAddress = new Uri("https://api." + options.Host + ".ru/" + options.Version + "/");
            if (possessionID != null)
            {
                possessionHash = Possession();
                if(possessionHash!=null)
                    clientApi.DefaultRequestHeaders.Add("X-Device-Possession-Token", possessionHash);
            }
            UpdateMHash();
            RepostTimeout = options.RepostTimeout;

            //clientSocket = new SocketIO("wss://ws-sio.dtf.ru/socket.io/?EIO=3&transport=websocket");
            //await StartAsync();
        }

        public async Task StartAsync()
        {
            clientSocket = new SocketIO("wss://ws-sio.dtf.ru/?EIO=3&transport=websocket");
            clientSocket.OnConnected += _socketIoClient_OnConnected;
            clientSocket.OnDisconnected += (_, e) => isConnected = false;
            clientSocket.OnError += (_, e) => isError = true;
            clientSocket.OnReconnecting += (_, e) => UpdateMHash();
            //clientSocket.OnPing += (_, e) => _logger.LogInformation("Ping");
            //clientSocket.OnPong += (_, e) => _logger.LogInformation($"Pong in {(int)e.TotalMilliseconds}ms");
            clientSocket.On("event", response =>
            {
                var data = response.GetValue<dynamic>();
                //string temp1 = (string)data.channel;
                //string temp2 = (string)data.data.author.id;
                //string temp3 = (string)data.data.type;
                if (((string)data.channel == "m:"+mHash) && ((string)data.data.action == "addMessage"))
                {
                    //string temp2 = (string)data.data.lastMessage.author.id;
                    if ((string)data.data.message.author.id != ID && (string)data.data.message.author.id != possessionID)
                    {
                        socketTasks.Enqueue(new User
                        {
                            id = (string)data.data.message.author.id,
                            username = (string)data.data.message.author.title,
                            imagePath = (string)data.data.message.author.picture,
                            lastMessageTime = (double)data.data.message.dtCreated,
                            lastMessage = (string)data.data.message.text
                        });
                    }
                }
                //_logger.LogInformation("Received event: " + (string)data.data.type);
            });
            await clientSocket.ConnectAsync();
        }

        private async void _socketIoClient_OnConnected(object sender, EventArgs e)
        {
            //_logger.LogInformation("Connected");
            isConnected = true;
            await clientSocket.EmitAsync("subscribe", new { channel = "m:"+mHash });
        }

        public async Task StopAsync(CancellationToken cancellationToken)
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
                return response.Content.ReadAsStringAsync().Result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return "error";
            }
        }
        private static async Task<string> RawPOSTHeaders(HttpClient client, string query, MultipartFormDataContent data) //Отправка POST-запроса с полученим чистого json
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

        private string Possession()
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
                    String.Format("\"{0}\"", keyValuePair.Key));
            }
            possessionID = (string)request.result.data.id;
            return RawPOSTHeaders(clientApi, "auth/possess", content).Result;
        }

        private void UpdateMHash()
        {
            var hashQuery = JsonConvert.DeserializeObject<dynamic>(RawGET(clientRaw, "https://dtf.ru/u/" + ID + "/stats?mode=ajax").Result)["module.auth"];
            mHash = hashQuery.m_hash;
            mHashLifetime = hashQuery.m_hash_expiration_time;
        }

        public void Listen() //"Слушает" входящие через запрос
        {
            LastResult = RawGET(clientApi, "m/counter").Result;
            if (LastResult == "error") LastStatus = -1;
            int.TryParse(string.Join("", LastResult.Where(c => char.IsDigit(c))), out LastStatus);
        }

        public void AnswerUser(string chanId, string answer) //Отправляет сообщение в указанный канал
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
                    String.Format("\"{0}\"", keyValuePair.Key));
            }
            LastResult = RawPOST(clientApi, "m/send", content).Result;
            //Console.WriteLine(result);
            MarkAsRead(chanId);
        }

        public void MarkAsRead(string chanId) //Отмечает все сообщения как прочитанные
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
                    String.Format("\"{0}\"", keyValuePair.Key));
            }
            LastResult = RawPOST(clientApi, "m/markAsRead", content).Result;
        }

        public bool isAuthor(string id, string link) //проверка авторства
        {
            return RawGET(clientApi, "locate?url="+link).Result.Contains("\"author\":{\"id\":"+id+",");
        }

        public string GetArticleID(string link) //получение идентификатора статьи
        {
            var request = JsonConvert.DeserializeObject<dynamic>(RawGET(clientApi, "locate?url=" + link).Result);
            return (string)request.result.data.id;
        }

        public MessageData RequestChannelsData() //Запрашивает информацию о входящих
        {
            MessageData temp = JsonConvert.DeserializeObject<MessageData>(RawGET(clientApi, "m/channels").Result);
            return temp;
        }
    }
}
