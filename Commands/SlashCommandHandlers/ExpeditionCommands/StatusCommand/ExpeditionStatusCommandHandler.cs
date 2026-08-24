using Discord;
using Discord.WebSocket;
using DiscordBot.Core;

namespace DiscordBot.Commands.SlashCommandHandlers.ExpeditionCommands.StatusCommand;

/// <summary>
///     A class containing the Handler for the /expedition status subcommand.
/// </summary>
public static class ExpeditionStatusCommandHandler
{
    /// <summary>
    ///     Handles the /expedition status subcommand, showing the remaining time (or claim
    ///     readiness) of the user's active expedition.
    /// </summary>
    /// <param name="command">
    ///     The slash command issued by the user, containing information about the command and the
    ///     user invoking it.
    /// </param>
    public static async Task Handle(SocketSlashCommand command)
    {
        // Defer the response to give more time to process
        await command.DeferAsync(ephemeral: true);

        if (!CommandHandler.BotActive)
        {
            await command.FollowupAsync("💤 Bot is inactive.", ephemeral: true);
            return;
        }

        var collection = await CardStorage.LoadUserCardsAsync(command.User.Id);
        var expedition = collection.ActiveExpedition;

        if (expedition == null)
        {
            await command.FollowupAsync("ℹ️ You don't have an expedition in progress. Use `/expedition start` to send cards off.", ephemeral: true);
            return;
        }

        var location = ExpeditionSettingsProvider.GetLocationById(expedition.LocationId);
        var locationName = location?.Name ?? expedition.LocationId;
        var endUnix = new DateTimeOffset(expedition.EndTimeUtc).ToUnixTimeSeconds();

        var cardList = string.Join("\n", expedition.SentCards.Select(c => $"`{c.Name}` ({c.Rarity})"));

        var remaining = expedition.EndTimeUtc - DateTime.UtcNow;

        var embed = new EmbedBuilder()
            .WithTitle($"🧭 Expedition to {locationName}")
            .AddField("Cards sent", string.IsNullOrEmpty(cardList) ? "—" : cardList)
            .AddField(
                remaining <= TimeSpan.Zero ? "Status" : "Time remaining",
                remaining <= TimeSpan.Zero
                    ? "✅ Ready to claim! Use `/expedition claim`."
                    : $"<t:{endUnix}:R> (<t:{endUnix}:f>)")
            .WithColor(remaining <= TimeSpan.Zero ? Color.Green : Color.Orange)
            .Build();

        await command.FollowupAsync(embed: embed, ephemeral: true);
    }
}
