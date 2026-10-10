using Discord;
using Discord.Interactions;
using DiscordBot.Core;
using DiscordBot.Models;
using DiscordBot.Preconditions;

namespace DiscordBot.Modules.Trade;

/// <summary>
///     The /trade, /confirmtrade and /canceltrade commands.
/// </summary>
[RequireBotActive]
public sealed class TradeModule(SessionStore sessions, BotStateStore stateStore, UserRepository users)
    : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("trade", "Propose a trade")]
    public async Task TradeAsync(
        [Summary("user", "The User you want to trade with.")] IUser targetUser,
        [Summary("give-card", "The name of the card you want to give.")] string giveCardName,
        [Summary("receive-card", "The name of the card you want to receive.")] string? receiveCardName = null,
        [Summary("receive-money", "The amount of money you want to receive.")] double receiveMoneyOption = 0.0)
    {
        await DeferAsync(ephemeral: false);

        decimal receiveMoney = Money.FromDouble(receiveMoneyOption);


        if (string.IsNullOrEmpty(receiveCardName) && receiveMoney <= 0)
        {
            await FollowupAsync("❌ You must specify either a card to receive or an amount of money (or both).", ephemeral: true);
            return;
        }

        if (targetUser.IsBot || targetUser.Id == Context.User.Id)
        {
            await FollowupAsync("❌ You cannot trade with yourself or a bot.", ephemeral: true);
            return;
        }

        // Checking for existing trade sessions
        if (sessions.IsTrading(Context.User.Id) || sessions.IsTrading(targetUser.Id))
        {
            await FollowupAsync("⚠️ One of the users is already in an active trade.", ephemeral: true);
            return;
        }

        // Validating Card Ownership (Sender)
        var senderCollection = await users.LoadUserCardsAsync(Context.User.Id);
        var cardToGive = FindTradableCopy(senderCollection, giveCardName);

        if (cardToGive == null)
        {
            await FollowupAsync($"❌ You don't own a card named `{giveCardName}`.", ephemeral: true);
            return;
        }

        if (cardToGive.IsLocked)
        {
            await FollowupAsync($"🔒 `{cardToGive.Name}` is locked (on an expedition) and can't be traded.", ephemeral: true);
            return;
        }

        var receiverCollection = await users.LoadUserCardsAsync(targetUser.Id);
        Card? cardToReceive = null;

        if (!string.IsNullOrEmpty(receiveCardName))
        {
            // Validate Card Ownership (Receiver)
            cardToReceive = FindTradableCopy(receiverCollection, receiveCardName);

            if (cardToReceive == null)
            {
                await FollowupAsync($"❌ {targetUser.Username} doesn't seem to own a `{receiveCardName}`.", ephemeral: true);
                return;
            }

            if (cardToReceive.IsLocked)
            {
                await FollowupAsync($"🔒 `{cardToReceive.Name}` is locked (on an expedition) and can't be traded.", ephemeral: true);
                return;
            }
        }

        if (receiveMoney > 0 && receiverCollection.Balance < receiveMoney)
        {
            await FollowupAsync($"❌ {targetUser.Username} doesn't have enough balance `{receiveMoney}` to fulfill this trade.", ephemeral: true);
            return;
        }

        sessions.AddTrade(new TradeSession(Context.User.Id, targetUser.Id, cardToGive, cardToReceive, receiveMoney));
        await stateStore.SaveAsync();

        var requestingText = string.Empty;
        if (cardToReceive != null) requestingText += $"`{cardToReceive.Name}`\n";
        if (receiveMoney > 0) requestingText += $"💰 {receiveMoney:F2}";

        var embed = new EmbedBuilder()
            .WithTitle("🤝 Trade Proposal")
            .WithDescription($"{Context.User.Mention} wants to trade with {targetUser.Mention}!")
            .AddField("Offering", $" `{cardToGive.Name}` ({cardToGive.Rarity})", true)
            .AddField("Requesting", requestingText, true)
            .WithFooter("Receiver must type /confirmtrade to finalize.")
            .WithColor(Color.Gold)
            .Build();

        await FollowupAsync(text: targetUser.Mention, embed: embed);
    }

    [SlashCommand("confirmtrade", "Accept the current trade")]
    public async Task ConfirmTradeAsync()
    {
        await DeferAsync(ephemeral: false);

        // Validate session existence
        if (!sessions.TryGetTrade(Context.User.Id, out TradeSession session))
        {
            await FollowupAsync("❌ You don't have any pending trades.", ephemeral: true);
            return;
        }

        // Ensure the person confirming is the intended receiver
        if (session.ReceiverId != Context.User.Id)
        {
            await FollowupAsync("⚠️ Only the person receiving the trade can confirm it.", ephemeral: true);
            return;
        }

        // Remove the entries so no one else can trigger this logic again
        sessions.RemoveTrade(session);
        await stateStore.SaveAsync();

        // Load both collections
        var senderCol = await users.LoadUserCardsAsync(session.SenderId);
        var receiverCol = await users.LoadUserCardsAsync(session.ReceiverId);

        var result = TradeSwap.Execute(session, senderCol, receiverCol);
        if (result.Failure is { } failure)
        {
            await FollowupAsync(DescribeFailure(failure, session));
            return;
        }

        var cardFromSender = result.CardFromSender!;
        var senderReceivedText = string.Empty;
        if (result.CardFromReceiver != null) senderReceivedText += $"`{result.CardFromReceiver.Name}`\n";
        if (session.MoneyToReceive > 0) senderReceivedText += $"💰 {session.MoneyToReceive:F2}\n";

        // One transaction: a crash between two writes can no longer duplicate or lose a card
        await users.SaveUserCardsAsync(senderCol, receiverCol);

        var embed = new EmbedBuilder()
            .WithTitle("✅ Trade Successful!")
            .WithDescription($"{MentionUtils.MentionUser(session.SenderId)} and {MentionUtils.MentionUser(session.ReceiverId)} have successfully traded.")
            .AddField("New Additions",
                $"{MentionUtils.MentionUser(session.SenderId)} received:\n{senderReceivedText}\n" +
                $"{MentionUtils.MentionUser(session.ReceiverId)} received:\n`{cardFromSender.Name}`")
            .WithColor(Color.Green)
            .WithCurrentTimestamp()
            .Build();

        await FollowupAsync(embed: embed);
    }

    [SlashCommand("canceltrade", "End the trade session")]
    public async Task CancelTradeAsync()
    {
        await DeferAsync(ephemeral: false);

        // Checks if the user is actually in a trade
        if (!sessions.TryGetTrade(Context.User.Id, out TradeSession session))
        {
            await FollowupAsync("❌ You don't have any pending trades to cancel.", ephemeral: true);
            return;
        }

        // Identifies the partner to notify them
        ulong partnerId = (Context.User.Id == session.SenderId) ? session.ReceiverId : session.SenderId;

        sessions.RemoveTrade(session);
        await stateStore.SaveAsync();

        var embed = new EmbedBuilder()
            .WithTitle("🚫 Trade Cancelled")
            .WithDescription($"{Context.User.Mention} has cancelled the trade with {MentionUtils.MentionUser(partnerId)}.")
            .WithColor(Color.Red)
            .WithCurrentTimestamp()
            .Build();

        await FollowupAsync(embed: embed);
    }

    /// <summary>
    ///     Finds a copy of the named card, preferring one that is not on an expedition. The trade
    ///     is bound to that exact copy.
    /// </summary>
    private static Card? FindTradableCopy(UserCardCollection collection, string cardName) =>
        collection.Cards
            .Where(c => c.Name.Equals(cardName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.IsLocked)
            .FirstOrDefault();

    private static string DescribeFailure(TradeFailure failure, TradeSession session) => failure switch
    {
        TradeFailure.SenderCardMissing => "❌ The sender's card is no longer in their inventory. Trade cancelled.",
        TradeFailure.SenderCardLocked => $"🔒 `{session.CardToTrade.Name}` got locked (on an expedition) in the meantime. Trade cancelled.",
        TradeFailure.ReceiverCardMissing => "❌ The requested card is no longer in your inventory. Trade cancelled.",
        TradeFailure.ReceiverCardLocked => $"🔒 `{session.CardToReceive?.Name}` got locked (on an expedition) in the meantime. Trade cancelled.",
        _ => "❌ You no longer have enough balance to complete this trade. Trade cancelled.",
    };
}
