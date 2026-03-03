using Discord;
using Discord.WebSocket;

namespace DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.HelpCommand;

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
    ///     the user
    /// </param>
    public async Task Handle(SocketSlashCommand command, string botAvatarUrl)
    {
        // Defer the response to give more time to process
        await command.DeferAsync(ephemeral: true);

        var embed = new EmbedBuilder()
        .WithTitle("📖 Bot Help Menu")
        .WithDescription("Catch 'em all and trade with friends! Here are the available commands:")
        .WithColor(Color.Blue)
        .WithThumbnailUrl(botAvatarUrl)

        .AddField("🎮 Trainer Commands",
            "`/pull [set-id]` - Open a pack of 9 cards from a specific set.\n" +
            "`/inventory` - Browse your saved cards and manage favorites.\n" +
            "`/profile [user]` - View your own or another trainer's collection stats.\n" +
            "`/sets` - **View all available Pokémon sets and find their specific IDs.**\n" +
            "`/stats` - View global bot statistics and uptime.")

        .AddField("🤝 Trading",
            "`/trade [user] [give-card] [receive-card]` - Propose a 1-for-1 card swap.\n" +
            "`/confirmtrade` - Accept the pending trade sent to you.\n" +
            "`/canceltrade` - Cancel your current active trade session.")

        .AddField("🛡️ Admin Commands",
            "`/lock [set-id]` | `/unlock` - Control which sets are currently pullable.\n" +
            "`/turnon` | `/turnoff` - Enable or disable bot command responses.\n" +
            "`/restart` - Perform a system reboot.")

        .WithFooter(footer => footer.Text = "Pokémon TCG Bot • Use slash commands to interact!")
        .WithCurrentTimestamp();

        await command.FollowupAsync(embed: embed.Build(), ephemeral: true);
        Console.WriteLine("Handled /help command successfully.");
    }
}