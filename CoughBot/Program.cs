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
using Microsoft.ML;
using Microsoft.ML.Data;
using Newtonsoft.Json;

namespace CoughBot
{
    class Program
    {
        private DiscordSocketClient _client;
        private MLContext _context;
        private ITransformer _mlModel;
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
            public List<InputData> MLData { get; set; } = new List<InputData>()
            {
                new InputData()
                {
                    MessageLength = 25,
                    CoughsPerSpan = 1,
                    InfectedAmount = 0.45f
                },
                new InputData()
                {
                    MessageLength = 25,
                    CoughsPerSpan = 10,
                    InfectedAmount = 0.25f
                },
                new InputData()
                {
                    MessageLength = 5,
                    CoughsPerSpan = 1,
                    InfectedAmount = 0.25f
                },
                new InputData()
                {
                    MessageLength = 100,
                    CoughsPerSpan = 1,
                    InfectedAmount = 0.85f
                }
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

        public class InputData
        {
            [LoadColumn(0)]
            public float MessageLength { get; set; }
            [LoadColumn(1)]
            public float CoughsPerSpan { get; set; }
            [LoadColumn(2)]
            public float InfectedAmount { get; set; }
        }

        public class OutputData
        {
            [ColumnName("PredictedLabel")]
            public float InfectedAmount { get; set; }
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

            _context = new MLContext(seed: 0);
            
            _mlModel = TrainBot();


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
                        databases = JsonConvert.DeserializeObject<Database>(await File.ReadAllTextAsync(path));
                        Console.WriteLine("Config reload...Done");
                        break;
                    case "svdb":
                        await SaveData();
                        Console.WriteLine("SaveData...Done");
                        break;
                    case "train":
                        _mlModel = TrainBot();
                        break;
                    case "rngtrain":
                        {
                            for (int i = 0; i < 25; i++)
                                databases.MLData.Add(new InputData()
                                {
                                    CoughsPerSpan = rng.Next(1, 5),
                                    MessageLength = rng.Next(5, 35),
                                    InfectedAmount = rng.Next(0, 1000) / 1000f
                                });
                            await SaveData();
                            _mlModel = TrainBot();
                        }
                        break;
                    case "exit":
                        await SaveData();
                        Console.WriteLine("Exit!");
                        return;
                }
            }
            // await Task.Delay(-1);
        }

        public ITransformer TrainBot()
        {
            IDataView data = _context.Data.LoadFromEnumerable(databases.MLData);
            var pipeLine = _context.Transforms.Concatenate("Features", nameof(InputData.MessageLength), nameof(InputData.CoughsPerSpan))
                .Append(_context.Transforms.Conversion.MapValueToKey("Label", nameof(InputData.InfectedAmount)));
            var trainer = pipeLine.Append(_context.MulticlassClassification.Trainers.SdcaMaximumEntropy(labelColumnName: "Label", featureColumnName: "Features", maximumNumberOfIterations: 100))
                .Append(_context.Transforms.Conversion.MapKeyToValue("PredictedLabel"));
            Console.WriteLine("Training bot... (this may take some time!)");
            Console.Out.Flush();
            var model = trainer.Fit(data);
            Console.WriteLine("Training bot...Done");
            return model;
        }

        public OutputData EvalBot(InputData data)
        {
            var prediction = _context.Model.CreatePredictionEngine<InputData, OutputData>(_mlModel);
            var eval = prediction.Predict(data);
            Console.WriteLine($"data.CoughsPerSpan {data.CoughsPerSpan}");
            Console.WriteLine($"data.MessageLength {data.MessageLength}");
            Console.WriteLine($"eval.InfectedAmount {eval.InfectedAmount}");
            return eval;
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
                        int leng = 0;
                        var msgsArr = msgs.Where(p => p.Content.Contains("*cough*") && (leng++ == 0 || true) && p.Author.Id == user1.Id && p.Timestamp.UtcDateTime.Add(TimeSpan.FromSeconds(config.SafeTimeSeconds)) >= DateTime.UtcNow).ToArray();
                        var msgsArrUseless = msgs.Where(p => (leng++ == 0 || true) && p.Author.Id != user1.Id && p.Timestamp.UtcDateTime.Add(TimeSpan.FromSeconds(config.SafeTimeSeconds)) >= DateTime.UtcNow && ((p.Author is SocketGuildUser p_suser && !p_suser.Roles.Contains(infectedRole)) || p.Author is not SocketGuildUser)).ToArray();
                        InputData mlData = new InputData()
                        {
                            CoughsPerSpan = msgsArr.Length,
                            MessageLength = leng
                        };
                        OutputData mlOut = EvalBot(mlData);
                        Console.WriteLine($"{user1.Username}#{user1.Discriminator} = {mlOut.InfectedAmount}");
                        if (mlOut.InfectedAmount >= config.InfectionMin)
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
                    var msgsa = message.Channel.GetMessagesAsync(config.InfectMessageLimit);
                    var msgs = await msgsa.FlattenAsync();
                    int leng = 0;
                    var msgsArr = msgs.Where(p => p.Content.Contains("*cough*") && (leng++ == 0 || true) && p.Author.Id == user1.Id && p.Timestamp.UtcDateTime.Add(TimeSpan.FromSeconds(config.SafeTimeSeconds)) >= DateTime.UtcNow).ToArray();
                    var msgsArrUseless = msgs.Where(p => (leng++ == 0 || true) && p.Author.Id != user1.Id && p.Timestamp.UtcDateTime.Add(TimeSpan.FromSeconds(config.SafeTimeSeconds)) >= DateTime.UtcNow && ((p.Author is SocketGuildUser p_suser && !p_suser.Roles.Contains(infectedRole)) || p.Author is not SocketGuildUser)).ToArray();
                    InputData mlData = new InputData()
                    {
                        CoughsPerSpan = msgsArr.Length,
                        MessageLength = leng
                    };
                    if (!found && databases.Guilds[user1.Guild.Id.ToString()].GuildUsers.Where(p => p.Infection >= config.InfectionMin).Count() < config.InfectionMaxPeopleRandom /*&& EvalBot(mlData).InfectedAmount >= config.InfectionMin*/)
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
