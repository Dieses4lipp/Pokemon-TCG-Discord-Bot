using Discord.WebSocket;

namespace DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.SetsCommand;

public static class SetsReactionHandler
{
    public static async Task Handle(SocketMessageComponent component, int page)
    {
        // Defer the response to give more time to process
        await component.DeferAsync(ephemeral: true);
    }
}