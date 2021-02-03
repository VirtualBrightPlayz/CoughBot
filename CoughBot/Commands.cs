using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;

namespace CoughBot
{
    public class Commands : ModuleBase<SocketCommandContext>
    {
        [Command("help")]
        [Summary("Lists all commands, can be used to list detailed info for certain commands.")]
        public async Task HelpAsync([Remainder][Summary("The command to show more info about")] string command = "")
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                EmbedBuilder emb = new EmbedBuilder();
                emb.Title = "Commands List";
                CommandService service = Program.program.GetCommandService();
                foreach (CommandInfo cmd in service.Commands)
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
    }
}