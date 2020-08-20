using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace DTF_message_bot
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleKeyInfo key = new ConsoleKeyInfo();
            Console.WriteLine("Бот для мессенджера Очобы\nСделано долбоёбом Neko Natum\n");
            //Инициализируем все классы перед работой
            BotConfig config = new BotConfig();
            MessageData data = new MessageData();
            Network worker = new Network();
            List<User> activeUsers = new List<User>();
            //

            config.readConfig();
            worker.setupNetworkToken(config.site, config.version, config.token);
            Console.WriteLine("Для завершения работы нажмите Ctrl+Z");

            var cts = new CancellationTokenSource();
            var consoleReaderThread = new Thread(() =>
            {
                while (Console.ReadKey(true).Key != ConsoleKey.Escape) { }
                cts.Cancel();
            });
            consoleReaderThread.Start();

            // есть еще такая фича, но на моем опыте не работала, плюс только Ctrl+C/Ctrl+Break в теории
            Console.CancelKeyPress += (_, __) => cts.Cancel();

            do
            {
                worker.Listen();
                if (worker.LastStatus > 0)
                {
                    data = worker.requestChannelsData();
                    foreach (Channels chan in data.result.channels)
                    {
                        if (chan.unreadCount != 0)
                        {
                            int currentActive;
                            if (!activeUsers.Exists(x => x.id == chan.id))
                            {
                                if (!File.Exists("users/" + chan.id + ".json"))
                                {
                                    activeUsers.Add(new User()
                                    {
                                        id = chan.id,
                                        username = chan.lastMessage.author.title,
                                        imagePath = chan.lastMessage.author.picture,
                                        lastMessageTime = chan.lastMessage.dtCreated,
                                        lastMessage = chan.lastMessage.text,
                                        lastAction = UserActions.Undefined,
                                        isCardExists = false,
                                        Description = null,
                                        links = null,
                                        tags = null
                                    });
                                    activeUsers.Last().SaveUserJson();
                                    Console.WriteLine("Создан новый пользователь с id = {0}", chan.id);
                                }
                                else
                                {
                                    activeUsers.Add(JsonSerializer.Deserialize<User>(File.ReadAllText("users/" + chan.id + ".json")));
                                    Console.WriteLine("Подключился пользователь с id = {0}", chan.id);
                                }
                                currentActive = activeUsers.Count - 1;
                            }
                            else
                            {
                                currentActive = activeUsers.FindIndex(x => string.Equals(x.id, chan.id));
                            }
                            activeUsers.ElementAt(currentActive).UpdateUser(chan);
                            //Console.WriteLine(chan.lastMessage.text);
                            //Console.WriteLine(activeUsers.ElementAt(currentActive).lastMessage);
                            activeUsers.ElementAt(currentActive).Actions(worker);
                        }
                    }
                }
                else if (worker.LastStatus == 0)
                {
                    //Console.WriteLine("Nothing to report!");
                    Thread.Sleep(1000);
                }
                else if (worker.LastStatus == -1)
                {
                    Console.WriteLine("Произошла ошибка сети, бот будет остановлен");
                    Thread.Sleep(1000);
                }
            }
            while (!cts.IsCancellationRequested && worker.LastStatus >= 0);

            foreach (User user in activeUsers)
            {
                user.SaveUserJson();
            }

            Console.WriteLine("Shutdown!");
        }

        
    }
}

