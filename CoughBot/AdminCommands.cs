using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;

namespace CoughBot
{
    [Group("admin")]
    public class AdminCommands : ModuleBase<SocketCommandContext>
    {
        [Command("registerguild")]
        [Summary("Registers the current Guild in the database.")]
        public async Task RegisterGuildAsync()
        {
            if (Context.User is SocketGuildUser guildUser && guildUser.GuildPermissions.ManageRoles)
            {
                await Program.program.RegisterGuild(Context.Guild, new Program.GuildConfig());
                await Context.Message.ReplyAsync("Guild registered.");
            }
            else
            {
                await Context.Message.ReplyAsync("Unable to register Guild: no permission.");
            }
        }
        
        [Command("setmaxmsginfect")]
        [Summary("Sets how many messages are able to be infected when coughing.")]
        public async Task MaxMessageInfectHistoryAsync([Summary("How many messages are able to be infected")] int messageInfectHistory = 5)
        {
            if (Context.User is SocketGuildUser guildUser && guildUser.GuildPermissions.ManageRoles)
            {
                Program.GuildConfig config = Program.program.GetGuildConfig(Context.Guild);
                if (config == null)
                {
                    await Context.Message.ReplyAsync("No config found on server.\nDid you register the bot?");
                    return;
                }
                config.InfectMessageLimit = messageInfectHistory;
                await Program.program.SaveData();
                await Context.Message.ReplyAsync($"Set max message history to {config.InfectMessageLimit}.");
            }
            else
            {
                await Context.Message.ReplyAsync("Unable to set max message history: no permission.");
            }
        }


        [Command("setmaxautoinfect")]
        [Summary("Sets the max auto infections that can happen.")]
        public async Task SetMaxAutoInfectAsync([Summary("After this many players are infected, random infection is no more")] int maxPlayersAutoInfect = 5)
        {
            if (Context.User is SocketGuildUser guildUser && guildUser.GuildPermissions.ManageRoles)
            {
                Program.GuildConfig config = Program.program.GetGuildConfig(Context.Guild);
                if (config == null)
                {
                    await Context.Message.ReplyAsync("No config found on server.\nDid you register the bot?");
                    return;
                }
                config.InfectionMaxPeopleRandom = maxPlayersAutoInfect;
                await Program.program.SaveData();
                await Context.Message.ReplyAsync($"Set max players auto infect to {config.InfectionMaxPeopleRandom}.");
            }
            else
            {
                await Context.Message.ReplyAsync("Unable to set max players auto infect: no permission.");
            }
        }

        [Command("setsafetimer")]
        [Summary("Sets the safe timer.")]
        public async Task SetSafeTimerAsync([Summary("How long until messages are considered 'safe' in seconds")] double safeTimer = 60d)
        {
            if (Context.User is SocketGuildUser guildUser && guildUser.GuildPermissions.ManageRoles)
            {
                Program.GuildConfig config = Program.program.GetGuildConfig(Context.Guild);
                if (config == null)
                {
                    await Context.Message.ReplyAsync("No config found on server.\nDid you register the bot?");
                    return;
                }
                config.SafeTimeSeconds = safeTimer;
                await Program.program.SaveData();
                await Context.Message.ReplyAsync($"Set safe timer to {config.SafeTimeSeconds} seconds.");
            }
            else
            {
                await Context.Message.ReplyAsync("Unable to set safe timer: no permission.");
            }
        }

        [Command("setautoinfect")]
        [Summary("Sets the chance for someone to be infected at random.")]
        public async Task SetAutoInfectAsync([Summary("0-100 The random percent chance for someone talking to be infected")] int autoInfectPrecent = 5)
        {
            if (Context.User is SocketGuildUser guildUser && guildUser.GuildPermissions.ManageRoles)
            {
                Program.GuildConfig config = Program.program.GetGuildConfig(Context.Guild);
                if (config == null)
                {
                    await Context.Message.ReplyAsync("No config found on server.\nDid you register the bot?");
                    return;
                }
                config.AutoInfectPercent = autoInfectPrecent;
                await Program.program.SaveData();
                await Context.Message.ReplyAsync($"Set auto infection percent to {config.AutoInfectPercent}.");
            }
            else
            {
                await Context.Message.ReplyAsync("Unable to set auto infection percent: no permission.");
            }
        }

        [Command("setrolecolor")]
        [Summary("Sets the infected role's color. Does not auto apply.")]
        public async Task SetRoleColorAsync(
            [Summary("0-255 Red Channel")] int roleRed = 255,
            [Summary("0-255 Green Channel")] int roleGreen = 165,
            [Summary("0-255 Blue Channel")] int roleBlue = 0)
        {
            if (Context.User is SocketGuildUser guildUser && guildUser.GuildPermissions.ManageRoles)
            {
                Program.GuildConfig config = Program.program.GetGuildConfig(Context.Guild);
                if (config == null)
                {
                    await Context.Message.ReplyAsync("No config found on server.\nDid you register the bot?");
                    return;
                }
                config.InfectedRoleColorRed = roleRed;
                config.InfectedRoleColorGreen = roleGreen;
                config.InfectedRoleColorBlue = roleBlue;
                await Program.program.SaveData();
                await Context.Message.ReplyAsync($"Set infected role color to (r:{config.InfectedRoleColorRed}, g:{config.InfectedRoleColorGreen}, b:{config.InfectedRoleColorBlue}).");
            }
            else
            {
                await Context.Message.ReplyAsync("Unable to set infected role color: no permission.");
            }
        }

        [Command("cureall")]
        [Summary("Cures everyone in the server (this deletes the infected role and clears stats).")]
        public async Task CureAllAsync()
        {
            if (Context.User is SocketGuildUser guildUser && guildUser.GuildPermissions.ManageRoles)
            {
                Program.GuildConfig config = Program.program.GetGuildConfig(Context.Guild);
                Program.GuildDatabase database = Program.program.GetGuildDatabase(Context.Guild);
                if (config == null || database == null)
                {
                    await Context.Message.ReplyAsync("No config/database found on server.\nDid you register the bot?");
                    return;
                }
                SocketRole infectedRole = Context.Guild.GetRole(config.InfectedRoleId);
                if (infectedRole != null)
                {
                    await infectedRole.DeleteAsync();
                }
                RestRole role = await Context.Guild.CreateRoleAsync(config.InfectedRoleName, GuildPermissions.None, new Discord.Color(config.InfectedRoleColorRed, config.InfectedRoleColorGreen, config.InfectedRoleColorBlue), false, false);
                config.InfectedRoleId = role.Id;
                database.GuildUsers.Clear();
                await Program.program.SaveData();
                await Context.Message.ReplyAsync($"{config.VirusName} has been contained.");
            }
            else
            {
                await Context.Message.ReplyAsync("Unable to cure all: no permission.");
            }
        }

        [Command("setvirusname")]
        [Summary("Sets the virus name on this Guild.")]
        public async Task SetVirusNameAsync([Remainder][Summary("The new name of the virus for this Guild")] string name)
        {
            if (Context.User is SocketGuildUser guildUser && guildUser.GuildPermissions.ManageRoles)
            {
                Program.GuildConfig config = Program.program.GetGuildConfig(Context.Guild);
                if (config == null)
                {
                    await Context.Message.ReplyAsync("No config found on server.\nDid you register the bot?");
                    return;
                }
                config.VirusName = name;
                await Program.program.SaveData();
                await Context.Message.ReplyAsync($"Set virus name to {config.VirusName}.");
            }
            else
            {
                await Context.Message.ReplyAsync("Unable to set virus name: no permission.");
            }
        }

        [Command("setinfectedname")]
        [Summary("Sets the name on the infected role (when created by the bot).")]
        public async Task SetInfectedNameAsync([Remainder][Summary("The new name of the infected role for this Guild")] string name)
        {
            if (Context.User is SocketGuildUser guildUser && guildUser.GuildPermissions.ManageRoles)
            {
                Program.GuildConfig config = Program.program.GetGuildConfig(Context.Guild);
                if (config == null)
                {
                    await Context.Message.ReplyAsync("No config found on server.\nDid you register the bot?");
                    return;
                }
                config.InfectedRoleName = name;
                await Program.program.SaveData();
                await Context.Message.ReplyAsync($"Set infected role name to {config.InfectedRoleName}.");
            }
            else
            {
                await Context.Message.ReplyAsync("Unable to set infected role name: no permission.");
            }
        }
        
        [Command("infect")]
        [Summary("Infects the specified user.")]
        public async Task InfectUserAsync(IGuildUser user)
        {
            if (Context.User is SocketGuildUser guildUser && guildUser.GuildPermissions.ManageRoles && user is SocketGuildUser guildUser1)
            {
                Program.GuildConfig config = Program.program.GetGuildConfig(Context.Guild);
                if (config == null)
                {
                    await Context.Message.ReplyAsync("No config found on server.\nDid you register the bot?");
                    return;
                }
                SocketRole infectedRole = Context.Guild.GetRole(config.InfectedRoleId);
                if (infectedRole == null)
                {
                    await Context.Message.ReplyAsync($"The role by id of {config.InfectedRoleId} not found.");
                    return;
                }
                await Program.program.InfectUser(guildUser1, infectedRole);
                await Context.Message.ReplyAsync($"{guildUser1.Username} is now infected.");
            }
        }
        
        [Command("cure")]
        [Summary("Cures the specified user.")]
        public async Task CureUserAsync(IGuildUser user)
        {
            if (Context.User is SocketGuildUser guildUser && guildUser.GuildPermissions.ManageRoles && user is SocketGuildUser guildUser1)
            {
                Program.GuildConfig config = Program.program.GetGuildConfig(Context.Guild);
                if (config == null)
                {
                    await Context.Message.ReplyAsync("No config found on server.\nDid you register the bot?");
                    return;
                }
                SocketRole infectedRole = Context.Guild.GetRole(config.InfectedRoleId);
                if (infectedRole == null)
                {
                    await Context.Message.ReplyAsync($"The role by id of {config.InfectedRoleId} not found.");
                    return;
                }
                await Program.program.CureUser(guildUser1, infectedRole);
                await Context.Message.ReplyAsync($"{guildUser1.Username} is now cured.");
            }
        }
    }
}