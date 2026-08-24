using Discord;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Events.Expedtion;
using DiscordBot.Models;

namespace DiscordBot.Commands.SlashCommandHandlers.ExpeditionCommands.StartCommand;

/// <summary>
///     A class containing the Handler for the /expedition start subcommand.
/// </summary>
public static class ExpeditionStartCommandHandler
{
    /// <summary>
    ///     Handles the /expedition start subcommand, validating card ownership and that no
    ///     expedition is already running before sending cards off.
    /// </summary>
    /// <param name="command">
    ///     The slash command issued by the user, containing information about the command and the
    ///     user invoking it.
    /// </param>
    /// <param name="subCommand">
    ///     The "start" subcommand option, containing the location and cards options.
    /// </param>
    public static async Task Handle(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        // Defer the response to give more time to process
        await command.DeferAsync(ephemeral: true);

        if (!CommandHandler.BotActive)
        {
            await command.FollowupAsync("💤 Bot is inactive.", ephemeral: true);
            return;
        }

        var locationId = subCommand.Options.FirstOrDefault(o => o.Name == "location")?.Value as string;
        var cardsInput = subCommand.Options.FirstOrDefault(o => o.Name == "cards")?.Value as string;

        var location = string.IsNullOrWhiteSpace(locationId)
            ? null
            : ExpeditionSettingsProvider.GetLocationById(locationId);

        if (location == null)
        {
            await command.FollowupAsync($"❌ Unknown expedition location `{locationId}`.", ephemeral: true);
            return;
        }

        var collection = await CardStorage.LoadUserCardsAsync(command.User.Id);

        // No expedition already running
        if (collection.ActiveExpedition != null)
        {
            await command.FollowupAsync("⚠️ You already have an expedition in progress. Use `/expedition status` to check on it.", ephemeral: true);
            return;
        }

        var requestedNames = (cardsInput ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (requestedNames.Count == 0)
        {
            await command.FollowupAsync("❌ You must specify at least one card to send.", ephemeral: true);
            return;
        }

        // Card ownership validation - resolve each requested name to a distinct, unlocked owned
        // card. Tracked by list index (not value equality) so duplicate identical copies can each
        // be selected once.
        var selectedCards = new List<Card>();
        var pickedIndices = new HashSet<int>();

        foreach (var name in requestedNames)
        {
            var matchIndex = collection.Cards.FindIndex(c =>
                c.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && !c.IsLocked);

            while (matchIndex != -1 && pickedIndices.Contains(matchIndex))
            {
                matchIndex = collection.Cards.FindIndex(matchIndex + 1, c =>
                    c.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && !c.IsLocked);
            }

            if (matchIndex == -1)
            {
                await command.FollowupAsync($"❌ You don't own an available (unlocked) card named `{name}`.", ephemeral: true);
                return;
            }

            pickedIndices.Add(matchIndex);
            selectedCards.Add(collection.Cards[matchIndex]);
        }

        // Lock the selected cards so they can't be sold/traded while away
        foreach (var card in selectedCards)
        {
            card.IsLocked = true;
        }

        var startTime = DateTime.UtcNow;
        var endTime = startTime.AddMinutes(location.DurationMinutes);

        collection.ActiveExpedition = new ActiveExpedition
        {
            LocationId = location.Id,
            StartTimeUtc = startTime,
            EndTimeUtc = endTime,
            SentCards = selectedCards,
        };

        await CardStorage.SaveUserCardsAsync(collection);

        await command.FollowupAsync(
            $"✅ Sent {selectedCards.Count} card(s) on an expedition to **{location.Name}**!\n" +
            $"Returns <t:{new DateTimeOffset(endTime).ToUnixTimeSeconds()}:R>.",
            ephemeral: true);
    }
}
