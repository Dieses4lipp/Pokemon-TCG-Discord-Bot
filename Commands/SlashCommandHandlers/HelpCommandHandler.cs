using Discord;
using Discord.WebSocket;

namespace DiscordBot.Commands.SlashCommandHandlers;

/// <summary>
///     A Class that handles the /help command, providing users with a list of available commands
///     and their descriptions.
/// </summary>
public class HelpCommandHandler
{
    /// <summary>
    ///     handles the /help command by sending an embedded message that lists the available
    /// </summary>
    /// <param name="command">
    ///     The <see cref="SocketSlashCommand"/> representing the slash command that was executed by
    /// </param>
    public async Task Handle(SocketSlashCommand command)
    {
        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle("Commands")
            .WithDescription("The following slash commands are available:")
            .AddField("/pull [Set-ID]", "Pulls a pack of 9 random Pokémon cards.")
            .AddField("/profile [@user]", "Displays a User's profile.")
            .AddField("/mycards", "Displays your saved Pokémon cards.")
            .AddField("/sets", "Displays a list of Pokémon card sets.")
            .AddField("/trade [@user] [card index]", "Initiates a trade with another user.")
            .AddField("/confirmtrade", "Confirms a trade with another user.")
            .AddField("/canceltrade", "Cancels a trade with another user.")
            .AddField("/unlock [Set-ID]", "Unlocks a specific set to allow it to be pulled again. (Admin only)")
            .AddField("/lock [Set-ID]", "Locks a specific set to prevent it from being pulled. (Admin only)")
            .AddField("/restart", "Restarts the bot. (Admin only)")
            .AddField("/turnon", "Turns on the bot to allow it to respond to commands. (Admin only)")
            .AddField("/turnoff", "Turns off the bot to prevent it from responding to commands. (Admin only)")
            .AddField("/stats", "Displays bot statistics. (Admin only)")
            .WithColor(Color.Blue);

        // Defer the response to give more time to process
        await command.DeferAsync(ephemeral: true);
        await command.FollowupAsync(ephemeral: true, embed: embed.Build());
        Console.WriteLine("Handled /help command successfully.");
    }
}