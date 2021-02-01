using System;
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
        private SocketRole infectedRole;
        private string path = "config.json";

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
            public double SafeTimeSeconds { get; set; } = 60.0d;
            public int InfectedRoleColorRed { get; set; } = 255;
            public int InfectedRoleColorGreen { get; set; } = 165;
            public int InfectedRoleColorBlue { get; set; } = 0;
            public int AutoInfectPercent { get; set; } = 20;
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

            config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));

            Console.WriteLine(JsonConvert.SerializeObject(config, Formatting.Indented));

            await _client.LoginAsync(TokenType.Bot, config.Token);
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task MessageReceived(SocketMessage arg)
        {
            if (arg is SocketUserMessage message && !message.Author.IsBot && !message.Author.IsWebhook && message.Author is SocketGuildUser user1)
            {
                if (infectedRole == null)
                {
                    infectedRole = user1.Guild.GetRole(config.InfectedRoleId);
                }
                if (infectedRole != null && !config.SafeChannelIds.Contains(message.Channel.Id) && !config.SuperSafeChannelIds.Contains(message.Channel.Id) && message.Content.ToLower().Contains("*cough*") && user1.Roles.Contains(infectedRole))
                {
                    var msgsa = message.Channel.GetMessagesAsync(config.InfectMessageLimit);
                    var msgs = await msgsa.FlattenAsync();
                    if (msgs.Where(p => p.Content.Contains("*cough*") && p.Author.Id == user1.Id).Count() != 0)
                    {
                        return;
                    }
                    IMessage[] array = msgs.Where(p => !p.Author.IsBot && !p.Author.IsWebhook && p.Author.Id != user1.Id && p.Timestamp.UtcDateTime.Add(TimeSpan.FromSeconds(config.SafeTimeSeconds)) >= DateTime.UtcNow && p.Author is SocketGuildUser user && !user.Roles.Contains(infectedRole)).ToArray();
                    if (array.Length != 0)
                    {
                        IMessage msg2 = array[rng.Next(array.Length)];
                        Console.WriteLine(msg2.GetType().FullName);
                        Console.WriteLine(msg2.Author.GetType().FullName);
                        if (msg2 is RestUserMessage message2 && message2.Author is SocketGuildUser user2)
                        {
                            await user2.AddRoleAsync(infectedRole);
                            string name = string.IsNullOrWhiteSpace(user1.Nickname) ? user1.Username : user1.Nickname;
                            await message2.ReplyAsync($"{name} infected you with {config.VirusName}!");
                        }
                    }
                }
                if (infectedRole != null && !user1.Roles.Contains(infectedRole) && !config.SuperSafeChannelIds.Contains(message.Channel.Id) && rng.Next(100) <= config.AutoInfectPercent)
                {
                    await user1.AddRoleAsync(infectedRole);
                    await message.ReplyAsync($"Somehow, you were infected with {config.VirusName}!");
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
                    await message.ReplyAsync($"{config.VirusName} has been contained.");
                }
            }
        }

        private async Task Log(LogMessage arg)
        {
            Console.WriteLine(arg.ToString());
        }
    }
}
