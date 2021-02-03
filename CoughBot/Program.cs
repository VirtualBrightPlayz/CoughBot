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
            public string InfectCommand { get; set; } = "/infect";
            public string CureCommand { get; set; } = "/cure";
            public string ResetCommand { get; set; } = "/cureall";
            public string StatsCommand { get; set; } = "/virusstats";
            public double SafeTimeSeconds { get; set; } = 60.0d;
            public int InfectedRoleColorRed { get; set; } = 255;
            public int InfectedRoleColorGreen { get; set; } = 165;
            public int InfectedRoleColorBlue { get; set; } = 0;
            public int AutoInfectPercent { get; set; } = 20;
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
            public List<ulong> InfectedWho { get; set; } = new List<ulong>();
        }

        static void Main(string[] args)
        {
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        public async Task MainAsync()
        {
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
                    // TODO: ML in the RNG infection
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
                }
                if (infectedRole != null && message.Content.ToLower().StartsWith(config.StatsCommand.ToLower()))
                {
                    string[] infected = infectedRole.Members.OrderByDescending(p =>
                    {
                        GuildUserDatabase dbUser = databases.Guilds[user1.Guild.Id.ToString()].GuildUsers.FirstOrDefault(p2 => p2.Id == p.Id);
                        if (dbUser != null && dbUser.Infection >= config.InfectionMin)
                            return dbUser.InfectedTimestamp;
                        return long.MinValue;
                    }).Select(p =>
                    {
                        string time = "N/A";
                        GuildUserDatabase dbUser = databases.Guilds[user1.Guild.Id.ToString()].GuildUsers.FirstOrDefault(p2 => p2.Id == p.Id);
                        if (dbUser != null && dbUser.Infection >= config.InfectionMin)
                            time = DateTime.MinValue.AddTicks(dbUser.InfectedTimestamp).ToString();
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
                    Dictionary<string, List<ulong>> stats = new Dictionary<string, List<ulong>>();
                    foreach (var uitem in databases.Guilds[user1.Guild.Id.ToString()].GuildUsers)
                    {
                        stats.Add(uitem.Id.ToString(), uitem.InfectedWho);
                    }
                    StatsDraw.Draw(path, user1.Guild, stats, config.StatsMaxInfectedListings);
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
                    databases.Guilds[user1.Guild.Id.ToString()].GuildUsers.Clear();
                    await SaveData();
                    await message.ReplyAsync($"{config.VirusName} has been contained.");
                }
                // TODO: add cure command
                /*if (user1.GuildPermissions.ManageRoles && message.Content.ToLower().StartsWith(config.CureCommand.ToLower()))
                {
                    if (infectedRole != null)
                    {
                        await infectedRole.DeleteAsync();
                    }
                    RestRole role = await user1.Guild.CreateRoleAsync(config.InfectedRoleName, GuildPermissions.None, new Discord.Color(config.InfectedRoleColorRed, config.InfectedRoleColorGreen, config.InfectedRoleColorBlue), false, false);
                    configs.Guilds[user1.Guild.Id.ToString()].InfectedRoleId = role.Id;
                    databases.Guilds[user1.Guild.Id.ToString()].GuildUsers.Clear();
                    await SaveData();
                    await message.ReplyAsync($"{config.VirusName} has been contained.");
                }*/
                if (message.Content.ToLower().StartsWith(config.InfectCommand.ToLower()))
                {
                    if (user1.GuildPermissions.ManageRoles && message.MentionedUsers.Count > 0)
                    {
                        List<string> usernames = new List<string>();
                        foreach (var ping in message.MentionedUsers)
                        {
                            var u = guild.GetUser(ping.Id);
                            if (u == null)
                                continue;
                            await InfectUser(u, infectedRole);
                            usernames.Add(u.Username);
                        }
                        await message.ReplyAsync($"Infected {string.Join(", ", usernames)} with {config.VirusName}.");
                    }
                    else
                    {
                        await InfectUser(user1, infectedRole);
                        await message.ReplyAsync($"Somehow, you were infected with {config.VirusName}!");
                    }
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

        private async Task Log(LogMessage arg)
        {
            Console.WriteLine(arg.ToString());
            await Task.Delay(0);
        }
    }
}
