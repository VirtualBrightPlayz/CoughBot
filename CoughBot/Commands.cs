using System;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

public class Commands
{
    private readonly DiscordSocketClient _client;
    private readonly CommandService _commands;


    public Commands(DiscordSocketClient client, CommandService commands)
    {
        _client = client;
        _commands = commands;
    }

    public async Task Install()
    {
        _client.MessageReceived += HandleCommand;

        await _commands.AddModuleAsync<Commands>(null);
    }

    private async Task HandleCommand(SocketMessage arg)
    {
        if (arg is SocketUserMessage message && !message.Author.IsBot && !message.Author.IsWebhook)
        {
            var ctx = new SocketCommandContext(_client, message);
            await _commands.ExecuteAsync(ctx, 0, null);
        }
    }
}