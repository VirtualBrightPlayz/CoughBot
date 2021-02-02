using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace CoughBot
{
    class Program
    {
        private DiscordSocketClient _client;
        private Random rng;
        private Config configs;
        private Database databases;
        // private SocketRole infectedRole;
        private string path = "config.json";
        private string dataPath = "data.json";

        public class Config
        {
            public string Token { get; set; } = "bot_token";
            public Dictionary<string, GuildConfig> Guilds { get; set; } = new Dictionary<string, GuildConfig>()
            {
                { "0", new GuildConfig() }
            };
        }

        public class GuildConfig
        {
            public ulong InfectedRoleId { get; set; }
            public ulong[] SafeChannelIds { get; set; } = new ulong[1];
            public ulong[] SuperSafeChannelIds { get; set; } = new ulong[1];
            public int InfectMessageLimit { get; set; } = 5;
            public string VirusName { get; set; } = "The Virus";
            public string InfectedRoleName { get; set; } = "The Infected";
            public string ResetCommand { get; set; } = "/cureall";
            public string StatsCommand { get; set; } = "/virusstats";
            public double SafeTimeSeconds { get; set; } = 60.0d;
            public int InfectedRoleColorRed { get; set; } = 255;
            public int InfectedRoleColorGreen { get; set; } = 165;
            public int InfectedRoleColorBlue { get; set; } = 0;
            public int AutoInfectPercent { get; set; } = 20;
            public int StatsMaxInfectedListings { get; set; } = 15;
        }

        public class Database
        {
            public Dictionary<string, GuildDatabase> Guilds { get; set; } = new Dictionary<string, GuildDatabase>()
            {
                { "0", new GuildDatabase() }
            };
        }

        public class GuildDatabase
        {
            public Dictionary<string, long> InfectedTimestamps { get; set; } = new Dictionary<string, long>();
            public Dictionary<string, List<ulong>> InfectedWho { get; set; } = new Dictionary<string, List<ulong>>();
        }

        static void Main(string[] args)
        {
            /*CommandThread = new Thread(new ThreadStart(ConsoleInputThread));
            CommandThread.IsBackground = true;
            CommandThread.Name = "Console Thread";
            CommandThread.Start();*/
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        static void ConsoleInputThread()
        {
            Thread.Sleep(1);
            string[] cmd = Console.ReadLine().Split(' ');

            switch (cmd[0].ToLower())
            {
                case "rlcfg":
                    break;
            }
        }

        public async Task MainAsync()
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                // LogLevel = LogSeverity.Verbose,
                AlwaysDownloadUsers = true,
                LargeThreshold = 10000,
                MessageCacheSize = 500
            });
            rng = new Random();

            _client.Log += Log;
            _client.MessageReceived += MessageReceived;

            if (!File.Exists(path))
                await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(new Config(), Formatting.Indented));

            if (!File.Exists(dataPath))
                await File.WriteAllTextAsync(dataPath, JsonConvert.SerializeObject(new Database(), Formatting.Indented));

            configs = JsonConvert.DeserializeObject<Config>(await File.ReadAllTextAsync(path));
            databases = JsonConvert.DeserializeObject<Database>(await File.ReadAllTextAsync(dataPath));

            Console.WriteLine(JsonConvert.SerializeObject(configs, Formatting.Indented));

            await _client.LoginAsync(TokenType.Bot, configs.Token);
            await _client.StartAsync();

            while (true)
            {
                await Task.Delay(1);
                string[] cmd = Console.ReadLine().Split(' ');

                switch (cmd[0].ToLower())
                {
                    case "rlcfg":
                        configs = JsonConvert.DeserializeObject<Config>(await File.ReadAllTextAsync(path));
                        databases = JsonConvert.DeserializeObject<Database>(await File.ReadAllTextAsync(path));
                        Console.WriteLine(JsonConvert.SerializeObject(configs, Formatting.Indented));
                        break;
                    case "svdb":
                        await SaveData();
                        break;
                    case "exit":
                        await SaveData();
                        return;
                }
            }
            // await Task.Delay(-1);
        }

        private async Task MessageReceived(SocketMessage arg)
        {
            SocketRole infectedRole = null;
            if (arg is SocketUserMessage message && !message.Author.IsBot && !message.Author.IsWebhook && message.Author is SocketGuildUser user1 && message.Channel is SocketGuildChannel channel)
            {
                if (!configs.Guilds.ContainsKey(user1.Guild.Id.ToString()))
                {
                    return;
                }
                if (!databases.Guilds.ContainsKey(user1.Guild.Id.ToString()))
                {
                    databases.Guilds.Add(user1.Guild.Id.ToString(), new GuildDatabase());
                }
                GuildConfig config = (configs.Guilds[user1.Guild.Id.ToString()]);
                if (infectedRole == null)
                {
                    infectedRole = user1.Guild.GetRole(config.InfectedRoleId);
                }
                SocketGuild guild = user1.Guild;
                if (infectedRole != null && !config.SafeChannelIds.Contains(message.Channel.Id) && !config.SuperSafeChannelIds.Contains(message.Channel.Id) && message.Content.ToLower().Contains("*cough*") && user1.Roles.Contains(infectedRole))
                {
                    bool found = false;
                    foreach (var item in config.SafeChannelIds)
                    {
                        var cat = guild.GetCategoryChannel(item);
                        if (cat == null || !cat.Channels.Contains(channel))
                            continue;
                        found = true;
                        break;
                    }
                    if (!found)
                    {
                        foreach (var item in config.SuperSafeChannelIds)
                        {
                            var cat = guild.GetCategoryChannel(item);
                            if (cat == null || !cat.Channels.Contains(channel))
                                continue;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        var msgsa = message.Channel.GetMessagesAsync(config.InfectMessageLimit);
                        var msgs = await msgsa.FlattenAsync();
                        // if (msgs.Where(p => p.Content.Contains("*cough*") && p.Author.Id == user1.Id).Count() == 0)
                        {
                            IMessage[] array = msgs.Where(p => !p.Author.IsBot && !p.Author.IsWebhook && p.Author.Id != user1.Id && p.Timestamp.UtcDateTime.Add(TimeSpan.FromSeconds(config.SafeTimeSeconds)) >= DateTime.UtcNow && p.Author is SocketGuildUser user && !user.Roles.Contains(infectedRole)).ToArray();
                            if (array.Length != 0)
                            {
                                IMessage msg2 = array[rng.Next(array.Length)];
                                if (msg2 is RestUserMessage message2 && message2.Author is SocketGuildUser user2)
                                {
                                    await InfectUser(user2, infectedRole, user1);
                                    await SaveData();
                                    string name = string.IsNullOrWhiteSpace(user1.Nickname) ? user1.Username : user1.Nickname;
                                    await message2.ReplyAsync($"{name} infected you with {config.VirusName}!");
                                }
                            }
                        }
                    }
                }
                if (infectedRole != null && !user1.Roles.Contains(infectedRole) && !config.SuperSafeChannelIds.Contains(message.Channel.Id) && rng.Next(100) < config.AutoInfectPercent)
                {
                    bool found = false;
                    foreach (var item in config.SuperSafeChannelIds)
                    {
                        var cat = guild.GetCategoryChannel(item);
                        if (cat == null || !cat.Channels.Contains(channel))
                            continue;
                        found = true;
                        break;
                    }
                    if (!found)
                    {
                        await InfectUser(user1, infectedRole);
                        await SaveData();
                        await message.ReplyAsync($"Somehow, you were infected with {config.VirusName}!");
                    }
                }
                if (infectedRole != null && message.Content.ToLower().StartsWith(config.StatsCommand.ToLower()))
                {
                    string[] infected = infectedRole.Members.OrderByDescending(p =>
                    {
                        if (databases.Guilds[user1.Guild.Id.ToString()].InfectedTimestamps.ContainsKey(p.Id.ToString()))
                            return databases.Guilds[user1.Guild.Id.ToString()].InfectedTimestamps[p.Id.ToString()];
                        return long.MinValue;
                    }).Select(p =>
                    {
                        string time = "N/A";
                        if (databases.Guilds[user1.Guild.Id.ToString()].InfectedTimestamps.ContainsKey(p.Id.ToString()))
                            time = DateTime.MinValue.AddTicks(databases.Guilds[user1.Guild.Id.ToString()].InfectedTimestamps[p.Id.ToString()]).ToString();
                        return $"{p.Username}#{p.Discriminator} was infected at {time}";
                    }).ToArray();
                    string output = "";
                    for (int i = 0; i < infected.Length && i < config.StatsMaxInfectedListings; i++)
                    {
                        if (i != 0)
                            output += "\n";
                        output += infected[i];
                    }
                    EmbedBuilder emb = new EmbedBuilder();
                    emb.WithColor(config.InfectedRoleColorRed, config.InfectedRoleColorGreen, config.InfectedRoleColorBlue);
                    emb.WithTitle(config.VirusName);
                    emb.WithDescription(output);
                    await message.ReplyAsync(embed: emb.Build());
                    // string path = Path.GetTempFileName();
                    string path = Path.GetRandomFileName() + ".png";
                    StatsDraw.Draw(path, user1.Guild, databases.Guilds[user1.Guild.Id.ToString()].InfectedWho, config.StatsMaxInfectedListings);
                    await message.Channel.SendFileAsync(path);
                    File.Delete(path);
                }
                if (user1.GuildPermissions.ManageRoles && message.Content.ToLower().StartsWith(config.ResetCommand.ToLower()))
                {
                    if (infectedRole != null)
                    {
                        await infectedRole.DeleteAsync();
                    }
                    RestRole role = await user1.Guild.CreateRoleAsync(config.InfectedRoleName, GuildPermissions.None, new Discord.Color(config.InfectedRoleColorRed, config.InfectedRoleColorGreen, config.InfectedRoleColorBlue), false, false);
                    configs.Guilds[user1.Guild.Id.ToString()].InfectedRoleId = role.Id;
                    databases.Guilds[user1.Guild.Id.ToString()].InfectedTimestamps.Clear();
                    databases.Guilds[user1.Guild.Id.ToString()].InfectedWho.Clear();
                    await SaveData();
                    await message.ReplyAsync($"{config.VirusName} has been contained.");
                }
            }
        }

        public async Task SaveData()
        {
            await File.WriteAllTextAsync(dataPath, JsonConvert.SerializeObject(databases, Formatting.Indented));
            await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(configs, Formatting.Indented));
        }

        public async Task InfectUser(SocketGuildUser user1, SocketRole infectedRole, SocketGuildUser infector = null)
        {
            if (infectedRole == null)
                return;
            await user1.AddRoleAsync(infectedRole);
            if (!databases.Guilds[user1.Guild.Id.ToString()].InfectedTimestamps.ContainsKey(user1.Guild.Id.ToString()))
                databases.Guilds[user1.Guild.Id.ToString()].InfectedTimestamps.Add(user1.Id.ToString(), DateTime.UtcNow.Ticks);
            if (infector != null)
            {
                if (!databases.Guilds[user1.Guild.Id.ToString()].InfectedWho.ContainsKey(infector.Id.ToString()))
                    databases.Guilds[user1.Guild.Id.ToString()].InfectedWho.Add(infector.Id.ToString(), new List<ulong>());
                databases.Guilds[user1.Guild.Id.ToString()].InfectedWho[infector.Id.ToString()].Add(user1.Id);
            }
            await SaveData();
        }

        private async Task Log(LogMessage arg)
        {
            Console.WriteLine(arg.ToString());
            await Task.Delay(0);
        }
    }
}
