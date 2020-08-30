using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

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
        public int LastStatus;
        public string LastResult;
        public string ID;
        public string mHash;
        public int mHashLifetime; 

        public OsnovaClient(IOptions<OsnovaOptions> optionsAccessor)
        {
            var options = optionsAccessor.Value;
            clientApi = new HttpClient();
            clientRaw = new HttpClient();
            clientApi.DefaultRequestHeaders.Add("X-Device-Token", options.Token);
            clientRaw.DefaultRequestHeaders.Add("Cookie", "osnova-remember="+options.osnova_remember+"; osnova-aid="+options.osnova_aid);
            //client.DefaultRequestHeaders.Add("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8");
            //client.DefaultRequestHeaders.Add("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
            //client.DefaultRequestHeaders.Add("accept-encoding", "gzip, deflate, br");
            //client.DefaultRequestHeaders.Add("accept-language", "en-US,en;q=0.9");
            //client.DefaultRequestHeaders.Add("cache-control", "no-cache");
            //client.DefaultRequestHeaders.Add("pragma", "no-cache");
            //client.DefaultRequestHeaders.Add("referer", "https://dtf.ru/");
            clientRaw.DefaultRequestHeaders.Add("user-agent", "Mozilla/5444.0");
            //client.DefaultRequestHeaders.Add("sec-fetch-dest", "empty");
            //client.DefaultRequestHeaders.Add("sec-fetch-mode", "cors");
            //client.DefaultRequestHeaders.Add("sec-fetch-site", "same-origin");
            clientRaw.DefaultRequestHeaders.Add("x-this-is-csrf", "THIS IS SPARTA!");
            ID = options.SelfID;
            UpdateMHash();
            clientApi.BaseAddress = new Uri("https://api." + options.Host + ".ru/" + options.Version + "/");

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
