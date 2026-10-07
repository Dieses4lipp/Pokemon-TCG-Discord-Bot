using Discord;
using Discord.Interactions;

namespace DiscordBot.Modules.Trainer;

/// <summary>
///     The /help command. Works while the bot is turned off.
/// </summary>
public sealed class HelpModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("help", "Lists all available commands.")]
    public async Task HelpAsync()
    {
        await DeferAsync(ephemeral: true);

        var embed = new EmbedBuilder()
        .WithTitle("📖 Bot Help Menu")
        .WithDescription("Catch 'em all and trade with friends! Here are the available commands:")
        .WithColor(Color.Blue)
        .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl())

        .AddField("🎮 Trainer Commands",
            "`/pull [set-id]` - Open a booster pack from a specific set (pack size and odds depend on the set).\n" +
            "`/inventory` - Browse your saved cards and manage favorites.\n" +
            "`/profile [user]` - View your own or another trainer's collection stats.\n" +
            "`/sets` - View all available Pokémon sets and find their specific IDs.\n" +
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

        await FollowupAsync(embed: embed.Build(), ephemeral: true);
    }
}
