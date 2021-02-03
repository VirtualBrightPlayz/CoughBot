using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace CoughBot
{
    public class Program
    {
        private DiscordSocketClient _client;
        private CommandService _service;
        private Random rng;
        private Config configs;
        private Database databases;
        private string path = "config.json";
        private string dataPath = "data.json";
        public static string Prefix { get; private set; } = "~~";
        public static Program program { get; private set; }

        public class Config
        {
            public string Token { get; set; } = "bot_token";
            [Obsolete]
            public string RegisterCommand { get; set; } = "/registerguild";
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
            [Obsolete]
            public string InfectCommand { get; set; } = "/infect";
            [Obsolete]
            public string CureCommand { get; set; } = "/cure";
            [Obsolete]
            public string ResetCommand { get; set; } = "/cureall";
            [Obsolete]
            public string StatsCommand { get; set; } = "/virusstats";
            public double SafeTimeSeconds { get; set; } = 60.0d;
            public int InfectedRoleColorRed { get; set; } = 255;
            public int InfectedRoleColorGreen { get; set; } = 165;
            public int InfectedRoleColorBlue { get; set; } = 0;
            public int AutoInfectPercent { get; set; } = 5;
            public int StatsMaxInfectedListings { get; set; } = 15;
            public float InfectionMin { get; set; } = 0.5f;
            public int InfectionMaxPeopleRandom { get; set; } = 5;
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
            public List<GuildUserDatabase> GuildUsers { get; set; } = new List<GuildUserDatabase>();
            [Obsolete("Use GuildUsers")]
            public Dictionary<string, long> InfectedTimestamps { get; set; } = new Dictionary<string, long>();
            [Obsolete("Use GuildUsers")]
            public Dictionary<string, List<ulong>> InfectedWho { get; set; } = new Dictionary<string, List<ulong>>();
        }

        public class GuildUserDatabase
        {
            public ulong Id { get; set; }
            public float Infection { get; set; }
            public long InfectedTimestamp { get; set; }
            public ulong LastMessage { get; set; }
            public List<ulong> InfectedWho { get; set; } = new List<ulong>();
        }

        static void Main(string[] args)
        {
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        public async Task MainAsync()
        {
            program = this;
            rng = new Random();

            if (!File.Exists(path))
                await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(new Config(), Formatting.Indented));

            if (!File.Exists(dataPath))
                await File.WriteAllTextAsync(dataPath, JsonConvert.SerializeObject(new Database(), Formatting.Indented));

            configs = JsonConvert.DeserializeObject<Config>(await File.ReadAllTextAsync(path));
            databases = JsonConvert.DeserializeObject<Database>(await File.ReadAllTextAsync(dataPath));

            _client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                // LogLevel = LogSeverity.Verbose,
                AlwaysDownloadUsers = true,
                LargeThreshold = 10000,
                MessageCacheSize = 500
            });

            _client.Log += Log;
            _client.MessageReceived += MessageReceived;

            _service = new CommandService();
            await _service.AddModuleAsync<Commands>(null);
            await _service.AddModuleAsync<AdminCommands>(null);

            await _client.LoginAsync(TokenType.Bot, configs.Token);
            await _client.StartAsync();

            while (true)
            {
                await Task.Delay(1);
                string[] cmd = Console.ReadLine().Split(' ');

                switch (cmd[0].ToLower())
                {
                    case "rlcfg":
                        Console.WriteLine("Config reload...");
                        configs = JsonConvert.DeserializeObject<Config>(await File.ReadAllTextAsync(path));
                        databases = JsonConvert.DeserializeObject<Database>(await File.ReadAllTextAsync(dataPath));
                        Console.WriteLine("Config reload...Done");
                        break;
                    case "svdb":
                        await SaveData();
                        Console.WriteLine("SaveData...Done");
                        break;
                    case "exit":
                        await SaveData();
                        Console.WriteLine("Exit!");
                        return;
                }
            }
            // await Task.Delay(-1);
        }

        public CommandService GetCommandService()
        {
            return _service;
        }

        private async Task RunCommandService(SocketUserMessage message)
        {
            int argPos = 0;
            if (!(message.HasStringPrefix(Prefix, ref argPos) || message.HasMentionPrefix(_client.CurrentUser, ref argPos)) || (message.Author.IsBot || message.Author.IsWebhook))
                return;
            SocketCommandContext context = new SocketCommandContext(_client, message);
            await _service.ExecuteAsync(context, argPos, null);
        }

        private async Task MessageReceived(SocketMessage arg)
        {
            SocketRole infectedRole = null;
            if (arg is SocketUserMessage message && !message.Author.IsBot && !message.Author.IsWebhook && message.Author is SocketGuildUser user1 && message.Channel is SocketGuildChannel channel)
            {
                if (!configs.Guilds.ContainsKey(user1.Guild.Id.ToString()))
                {
                    await RunCommandService(message);
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
                    return;
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
                    if (!found && databases.Guilds[user1.Guild.Id.ToString()].GuildUsers.Where(p => p.Infection >= config.InfectionMin).Count() < config.InfectionMaxPeopleRandom)
                    {
                        await InfectUser(user1, infectedRole);
                        await message.ReplyAsync($"Somehow, you were infected with {config.VirusName}!");
                    }
                    return;
                }
                await RunCommandService(message);
            }
        }

        public async Task RegisterGuild(SocketGuild guild, GuildConfig conf)
        {
            if (databases.Guilds.ContainsKey(guild.Id.ToString()))
            {
                databases.Guilds[guild.Id.ToString()] = new GuildDatabase();
            }
            else
            {
                databases.Guilds.Add(guild.Id.ToString(), new GuildDatabase());
            }
            if (configs.Guilds.ContainsKey(guild.Id.ToString()))
            {
                configs.Guilds[guild.Id.ToString()] = conf;
            }
            else
            {
                configs.Guilds.Add(guild.Id.ToString(), conf);
            }
            await SaveData();
        }

        public GuildConfig GetGuildConfig(SocketGuild guild)
        {
            if (configs.Guilds.ContainsKey(guild.Id.ToString()))
            {
                return configs.Guilds[guild.Id.ToString()];
            }
            return null;
        }

        public GuildDatabase GetGuildDatabase(SocketGuild guild)
        {
            if (databases.Guilds.ContainsKey(guild.Id.ToString()))
            {
                return databases.Guilds[guild.Id.ToString()];
            }
            return null;
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
            if (databases.Guilds[user1.Guild.Id.ToString()].GuildUsers.FirstOrDefault(p => p.Id == user1.Id) == null)
                databases.Guilds[user1.Guild.Id.ToString()].GuildUsers.Add(new GuildUserDatabase() { Id = user1.Id });
            GuildUserDatabase gu1 = databases.Guilds[user1.Guild.Id.ToString()].GuildUsers.FirstOrDefault(p => p.Id == user1.Id);
            gu1.InfectedTimestamp = DateTime.UtcNow.Ticks;
            gu1.Infection = 1f; // TODO: proper infection system
            
            if (infector != null)
            {
                if (databases.Guilds[user1.Guild.Id.ToString()].GuildUsers.FirstOrDefault(p => p.Id == infector.Id) == null)
                    databases.Guilds[user1.Guild.Id.ToString()].GuildUsers.Add(new GuildUserDatabase() { Id = infector.Id });
                GuildUserDatabase gu2 = databases.Guilds[user1.Guild.Id.ToString()].GuildUsers.FirstOrDefault(p => p.Id == infector.Id);
                gu2.InfectedWho.Add(user1.Id);
                // TODO: reduce infection for infector and make use of it properly
            }
            await SaveData();
        }

        public async Task CureUser(SocketGuildUser user1, SocketRole infectedRole)
        {
            if (infectedRole == null)
                return;
            await user1.RemoveRoleAsync(infectedRole);
            GuildUserDatabase db = databases.Guilds[user1.Guild.Id.ToString()].GuildUsers.FirstOrDefault(p => p.Id == user1.Id);
            if (db == null)
            {
                return;
            }
            databases.Guilds[user1.Guild.Id.ToString()].GuildUsers.Remove(db);
            await SaveData();
        }

        private async Task Log(LogMessage arg)
        {
            Console.WriteLine(arg.ToString());
            await Task.Delay(0);
        }
    }
}
