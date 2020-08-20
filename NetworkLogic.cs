using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http;
using System.Text.Json;

namespace DTF_message_bot
{
    class Network
    {
        static HttpClient client = new HttpClient();
        public int LastStatus;
        public string LastResult;

        public void setupNetworkToken(string site, string apiVer, string token) //Настройка работы по токену
        {
            client.DefaultRequestHeaders.Add("X-Device-Token", token);
            client.BaseAddress = new Uri("https://api."+site+".ru/"+apiVer+"/");
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

        public void Listen() //"Слушает" входящие через запрос
        {
            LastResult = RawGET(client, "m/counter").Result;
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
            LastResult = RawPOST(client, "m/send", content).Result;
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
            LastResult = RawPOST(client, "m/markAsRead", content).Result;
        }
        public MessageData requestChannelsData() //Запрашивает информацию о входящих
        {
            return JsonSerializer.Deserialize<MessageData>(RawGET(client, "m/channels").Result);
        }
    }
}
