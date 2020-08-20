using System;
using System.IO;
using System.Text.Json;

namespace DTF_message_bot
{
    public class BotConfig
    {
        public string site { get; set; }
        public string version { get; set; }
        public string token { get; set; }

        public void readConfig()
        {
            if (System.IO.File.Exists("config.json"))
            {
                BotConfig tmp = new BotConfig();
                StreamReader read = new StreamReader("config.json");
                tmp = JsonSerializer.Deserialize<BotConfig>(read.ReadToEnd());
                site = tmp.site;
                version = tmp.version;
                token = tmp.token;
            }
            else
            {
                Console.WriteLine("Файл конфигурации сети не создан. Необходимо записать настройки");
                Console.Write("Введите для какого сайта планируется бот (dtf/vc/tjournal): ");
                site = Console.ReadLine().ToString();
                Console.Write("Введите номер версии API (например, \"v1.9\"): ");
                version = Console.ReadLine().ToString();
                Console.Write("Введите токен авторизации: ");
                token = Console.ReadLine().ToString();
                //string temp = "{ \"site\":\"" + site+ "\",\"version\":\"" + version+ "\",\"token\":\"" + token+ "\"}";//да долбоёб. да стыдно
                File.WriteAllText("config.json", JsonSerializer.Serialize<BotConfig>(this));
            }
            Console.WriteLine("Настройки доступа приняты");
        }
    }
}
