using Discord;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Models;

namespace DiscordBot.Commands.SlashCommandHandlers.TradeCommands.TradeCommand;

/// <summary>
///     A class containing the Handler for the /trade command
/// </summary>
public static class TradeCommandHandler
{
    /// <summary>
    ///     The Handler for the /trade command, initiating a trade
    /// </summary>
    /// <param name="command">
    ///     The slash command issued by the user, containing information about the command and the
    ///     user invoking it.
    /// </param>
    public static async Task Handle(SocketSlashCommand command)
    {
        // Defer the response to give more time to process
        await command.DeferAsync(ephemeral: false);

        if (!CommandHandler.BotActive)
        {
            await command.FollowupAsync("💤 Bot is inactive.", ephemeral: true);
            return;
        }

        var giveCardName = command.Data.Options.FirstOrDefault(c => c.Name == "give-card")?.Value as string;
        var receiveCardName = command.Data.Options.FirstOrDefault(c => c.Name == "receive-card")?.Value as string;

        if (command.Data.Options.FirstOrDefault(x => x.Name == "user")?.Value is not SocketUser targetUser || targetUser.IsBot || targetUser.Id == command.User.Id)
        {
            await command.FollowupAsync("❌ You cannot trade with yourself or a bot.", ephemeral: true);
            return;
        }

        // Checking for existing trade sessions
        if (CommandHandler.ActiveTrades.ContainsKey(command.User.Id) || CommandHandler.ActiveTrades.ContainsKey(targetUser.Id))
        {
            await command.FollowupAsync("⚠️ One of the users is already in an active trade.", ephemeral: true);
            return;
        }

        // Validating Card Ownership (Sender)
        var senderCollection = await CardStorage.LoadUserCardsAsync(command.User.Id);
        var cardToGive = senderCollection.Cards.FirstOrDefault(c => c.Name.Equals(giveCardName, StringComparison.OrdinalIgnoreCase));

        if (cardToGive == null)
        {
            await command.FollowupAsync($"❌ You don't own a card named `{giveCardName}`.", ephemeral: true);
            return;
        }

        // Validate Card Ownership (Receiver)
        var receiverCollection = await CardStorage.LoadUserCardsAsync(targetUser.Id);
        var cardToReceive = receiverCollection.Cards.FirstOrDefault(c => c.Name.Equals(receiveCardName, StringComparison.OrdinalIgnoreCase));

        if (cardToReceive == null)
        {
            await command.FollowupAsync($"❌ {targetUser.Username} doesn't seem to own a `{receiveCardName}`.", ephemeral: true);
            return;
        }

        // Creates Session
        var tradeSession = new TradeSession(command.User.Id, targetUser.Id, cardToGive, cardToReceive);

        // Maps both users to the same session object
        CommandHandler.ActiveTrades[command.User.Id] = tradeSession;
        CommandHandler.ActiveTrades[targetUser.Id] = tradeSession;

        var embed = new EmbedBuilder()
            .WithTitle("🤝 Trade Proposal")
            .WithDescription($"{command.User.Mention} wants to trade with {targetUser.Mention}!")
            .AddField("Offering", $" `{cardToGive.Name}` ({cardToGive.Rarity})", true)
            .AddField("Requesting", $"`{cardToReceive.Name}`", true)
            .WithFooter("Receiver must type /confirmtrade to finalize.")
            .WithColor(Color.Gold)
            .Build();

        await command.FollowupAsync(text: targetUser.Mention, embed: embed);
    }
}