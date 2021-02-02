using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private Config config;
        private Database database;
        private SocketRole infectedRole;
        private string path = "config.json";
        private string dataPath = "data.json";

        public class Config
        {
            public string Token { get; set; } = "bot_token";
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
            public Dictionary<ulong, DateTime> InfectedTimestamps = new Dictionary<ulong, DateTime>();
        }

        static void Main(string[] args)
        {
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        public async Task MainAsync()
        {
            _client = new DiscordSocketClient();
            rng = new Random();

            _client.Log += Log;
            _client.MessageReceived += MessageReceived;

            if (!File.Exists(path))
                File.WriteAllText(path, JsonConvert.SerializeObject(new Config(), Formatting.Indented));

            if (!File.Exists(dataPath))
                File.WriteAllText(dataPath, JsonConvert.SerializeObject(new Database(), Formatting.Indented));

            config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
            database = JsonConvert.DeserializeObject<Database>(File.ReadAllText(path));

            Console.WriteLine(JsonConvert.SerializeObject(config, Formatting.Indented));

            await _client.LoginAsync(TokenType.Bot, config.Token);
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task MessageReceived(SocketMessage arg)
        {
            if (arg is SocketUserMessage message && !message.Author.IsBot && !message.Author.IsWebhook && message.Author is SocketGuildUser user1 && message.Channel is SocketGuildChannel channel)
            {
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
                        if (msgs.Where(p => p.Content.Contains("*cough*") && p.Author.Id == user1.Id).Count() == 0)
                        {
                            IMessage[] array = msgs.Where(p => !p.Author.IsBot && !p.Author.IsWebhook && p.Author.Id != user1.Id && p.Timestamp.UtcDateTime.Add(TimeSpan.FromSeconds(config.SafeTimeSeconds)) >= DateTime.UtcNow && p.Author is SocketGuildUser user && !user.Roles.Contains(infectedRole)).ToArray();
                            if (array.Length != 0)
                            {
                                IMessage msg2 = array[rng.Next(array.Length)];
                                Console.WriteLine(msg2.GetType().FullName);
                                Console.WriteLine(msg2.Author.GetType().FullName);
                                if (msg2 is RestUserMessage message2 && message2.Author is SocketGuildUser user2)
                                {
                                    await user2.AddRoleAsync(infectedRole);
                                    database.InfectedTimestamps.Add(user1.Id, DateTime.UtcNow);
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
                        await user1.AddRoleAsync(infectedRole);
                        database.InfectedTimestamps.Add(user1.Id, DateTime.UtcNow);
                        await SaveData();
                        await message.ReplyAsync($"Somehow, you were infected with {config.VirusName}!");
                    }
                }
                if (infectedRole != null && message.Content.ToLower().StartsWith(config.StatsCommand.ToLower()))
                {
                    string[] infected = infectedRole.Members.OrderByDescending(p =>
                    {
                        if (database.InfectedTimestamps.ContainsKey(p.Id))
                            return database.InfectedTimestamps[p.Id].Ticks;
                        return long.MinValue;
                    }).Select(p =>
                    {
                        string time = "N/A";
                        if (database.InfectedTimestamps.ContainsKey(p.Id))
                            time = database.InfectedTimestamps[p.Id].ToString();
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
                }
                if (user1.GuildPermissions.ManageRoles && message.Content.ToLower().StartsWith(config.ResetCommand.ToLower()))
                {
                    if (infectedRole != null)
                    {
                        foreach (var member in infectedRole.Members)
                        {
                            await member.RemoveRoleAsync(infectedRole);
                            await Task.Delay(100);
                        }
                    }
                    database.InfectedTimestamps.Clear();
                    await SaveData();
                    await message.ReplyAsync($"{config.VirusName} has been contained.");
                }
            }
        }

        public async Task SaveData()
        {
            await File.WriteAllTextAsync(dataPath, JsonConvert.SerializeObject(database, Formatting.Indented));
        }

        private async Task Log(LogMessage arg)
        {
            Console.WriteLine(arg.ToString());
        }
    }
}
