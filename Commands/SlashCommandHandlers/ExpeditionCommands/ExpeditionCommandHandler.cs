using Discord.WebSocket;
using DiscordBot.Commands.SlashCommandHandlers.ExpeditionCommands.StartCommand;
using DiscordBot.Commands.SlashCommandHandlers.ExpeditionCommands.StatusCommand;

namespace DiscordBot.Commands.SlashCommandHandlers.ExpeditionCommands;

/// <summary>
///     A class that dispatches the /expedition command to the correct subcommand handler.
/// </summary>
public static class ExpeditionCommandHandler
{
    /// <summary>
    ///     Handles the /expedition command, routing to the subcommand handler based on the
    ///     invoked subcommand name.
    /// </summary>
    /// <param name="command">
    ///     The slash command issued by the user, containing information about the command and the
    ///     user invoking it.
    /// </param>
    public static async Task Handle(SocketSlashCommand command)
    {
        var subCommand = command.Data.Options.FirstOrDefault();

        if (subCommand == null) return;

        switch (subCommand.Name)
        {
            case "start":
                await ExpeditionStartCommandHandler.Handle(command, subCommand);
                break;

            case "status":
                await ExpeditionStatusCommandHandler.Handle(command);
                break;
        }
    }
}
