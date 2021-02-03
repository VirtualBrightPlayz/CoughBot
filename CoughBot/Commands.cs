using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using static CoughBot.Program;

namespace CoughBot
{
    public class Commands : ModuleBase<SocketCommandContext>
    {
        public static int HelpPageSize { get; private set; } = 6;

        [Command("help")]
        [Summary("Lists all commands, can be used to list detailed info for certain commands.")]
        public async Task HelpAsync([Summary("Page number")] int page = 1, [Remainder][Summary("The command to show more info about")] string command = "")
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                CommandService service = Program.program.GetCommandService();
                EmbedBuilder emb = new EmbedBuilder();
                int pageNum = page - 1;
                emb.Title = $"Commands List Page {page}/{(int)Math.Round((float)service.Commands.Count() / HelpPageSize)}";
                List<CommandInfo> cmds = service.Commands.ToList().GetRange(Math.Clamp(pageNum * HelpPageSize, 0, service.Commands.Count()), Math.Clamp(HelpPageSize, 0, service.Commands.Count() - pageNum * HelpPageSize));
                foreach (CommandInfo cmd in cmds)
                {
                    emb.AddField($"`{Program.Prefix}{cmd.Aliases.First()}`", $"```css\n> {cmd.Summary}```");
                }
                await Context.Message.ReplyAsync(embed: emb.Build());
            }
            else
            {
                EmbedBuilder emb = new EmbedBuilder();
                emb.Title = $"Command {command}";
                CommandService service = Program.program.GetCommandService();
                foreach (CommandInfo cmd in service.Commands)
                {
                    if (cmd.Aliases.First() == command)
                    {
                        emb.Description = $"```css\n> {cmd.Summary}```";
                        foreach (ParameterInfo pi in cmd.Parameters)
                        {
                            emb.AddField($"`Parameter {pi.Name} (default: {pi.DefaultValue.ToString()})`", $"```css\n> {pi.Summary}```");
                        }
                        await Context.Message.ReplyAsync(embed: emb.Build());
                        return;
                    }
                }
                emb.Description = $"Command {command} not found.";
                await Context.Message.ReplyAsync(embed: emb.Build());
            }
        }

        [Command("stats")]
        [Summary("Gives a summary of recently diagnosed and who has been spreading the disease the most.")]
        public async Task StatsAsync()
        {
            Program.GuildConfig config = Program.program.GetGuildConfig(Context.Guild);
            Program.GuildDatabase database = Program.program.GetGuildDatabase(Context.Guild);
            if (config == null || database == null)
            {
                await Context.Message.ReplyAsync("No config/database found on server.\nDid an admin register the bot?");
                return;
            }
            SocketRole infectedRole = Context.Guild.GetRole(config.InfectedRoleId);
            string[] infected = infectedRole.Members.OrderByDescending(p =>
            {
                GuildUserDatabase dbUser = database.GuildUsers.FirstOrDefault(p2 => p2.Id == p.Id);
                if (dbUser != null && dbUser.Infection >= config.InfectionMin)
                    return dbUser.InfectedTimestamp;
                return long.MinValue;
            }).Select(p =>
            {
                string time = "N/A";
                GuildUserDatabase dbUser = database.GuildUsers.FirstOrDefault(p2 => p2.Id == p.Id);
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
            await Context.Message.ReplyAsync(embed: emb.Build());
            // string path = Path.GetTempFileName();
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".png");
            Dictionary<string, List<ulong>> stats = new Dictionary<string, List<ulong>>();
            foreach (var uitem in database.GuildUsers)
            {
                stats.Add(uitem.Id.ToString(), uitem.InfectedWho);
            }
            StatsDraw.Draw(path, Context.Guild, stats, config.StatsMaxInfectedListings);
            await Context.Channel.SendFileAsync(path);
            File.Delete(path);
        }
    }
}