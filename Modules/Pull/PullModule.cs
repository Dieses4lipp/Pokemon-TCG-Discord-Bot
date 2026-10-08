using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Models;
using DiscordBot.Preconditions;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Modules.Pull;

/// <summary>
///     The /pull command and the buttons on the pulled pack. Pack size, rarity odds and price are
///     configured per set in Data/packSettings.json.
/// </summary>
[RequireBotActive]
public sealed class PullModule(
    BotState botState,
    BotStateStore stateStore,
    SessionStore sessions,
    UserRepository users,
    SetCardCache setCards,
    PackSettingsProvider packSettings,
    ILogger<PullModule> logger) : InteractionModuleBase<SocketInteractionContext>
{
    private SocketMessageComponent Component => (SocketMessageComponent)Context.Interaction;

    [SlashCommand("pull", "Pulls a pack with given set ID.")]
    public async Task PullAsync(
        [Summary("set-id", "The ID of the set to pull from."), Autocomplete(typeof(SetIdAutocompleteHandler))]
        string setId,
        [Summary("language", "The language of displayed cards. (default is english)"), Autocomplete(typeof(LanguageAutocompleteHandler))]
        string lang = "en")
    {
        await DeferAsync(ephemeral: true);

        // Safety check for null or locked sets
        if (string.IsNullOrWhiteSpace(setId) || botState.IsSetLocked(setId))
        {
            await FollowupAsync($"🔒 Set `{setId ?? "Unknown"}` is locked or unavailable.", ephemeral: true);
            return;
        }

        try
        {
            // Pack layout (card count, slot odds, price) and covers come from Data/packSettings.json
            PackProfile profile = packSettings.GetProfile(setId);
            decimal packCost = Money.FromDouble(profile.PackPrice);

            // Check the balance before loading any cards, so broke users cost no API calls
            if (!await HasEnoughBalanceAsync(packCost))
                return;

            var allCards = await setCards.GetSetCardsAsync(setId, lang);

            if (allCards.Count == 0)
            {
                await FollowupAsync($"❌ No cards found for set: {setId}!", ephemeral: true);
                return;
            }

            Func<Card, string>? rarityOf = null;
            if (!lang.Equals("en", StringComparison.OrdinalIgnoreCase))
                rarityOf = PackBuilder.EnglishRarityLookup(await setCards.GetSetCardsAsync(setId, "en"));

            var random = new Random();

            var uncoveredRarities = PackBuilder.GetUncoveredRarities(profile, allCards, rarityOf);
            if (uncoveredRarities.Count > 0)
                logger.LogWarning("Pack settings: set '{SetId}' has rarities no slot can roll: {Rarities}",
                    setId, string.Join(", ", uncoveredRarities));

            var selectedCardList = PackBuilder.BuildPack(profile, allCards, random, rarityOf);

            string packImagePath = packSettings.GetRandomCoverPath(setId, random);

            if (!File.Exists(packImagePath))
            {
                await FollowupAsync($"❌ Pack image not found for set: {setId}", ephemeral: true);
                return;
            }

            // Reload: loading the set can take a while and the balance may have changed meanwhile
            var userCollection = await users.LoadUserCardsAsync(Context.User.Id);
            if (userCollection.Balance < packCost)
            {
                await SendInsufficientBalanceAsync(packCost, userCollection.Balance);
                return;
            }

            var packEmbed = new EmbedBuilder()
                .WithTitle($"")
                .WithImageUrl($"attachment://{Path.GetFileName(packImagePath)}")
                .WithColor(Color.Blue)
                .Build();

            var openPackButton = new ComponentBuilder()
                .WithButton("Open Pack", "open_pack", ButtonStyle.Primary, new Emoji("🎁"))
                .Build();

            var response = await Context.Interaction.FollowupWithFileAsync(
                packImagePath,
                embed: packEmbed,
                ephemeral: true,
                components: openPackButton
            );

            // Store pack session with cards ready to be revealed
            sessions.Packs[response.Id] = new PackSession(response.Id, Context.User.Id, selectedCardList);

            // Update Stats
            userCollection.PacksPulled++;
            botState.IncrementPullCount();
            // Deduct pack cost
            userCollection.Balance -= packCost;
            await users.SaveUserCardsAsync(userCollection);
            await stateStore.SaveAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to pull a pack of set {SetId} ({Language})", setId, lang);
            await FollowupAsync("⚠️ System error while opening pack.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles the "Open Pack" button click to reveal the cards.
    /// </summary>
    [ComponentInteraction("open_pack")]
    public async Task OpenPackAsync()
    {
        await DeferAsync(ephemeral: true);

        if (!sessions.Packs.TryGetValue(Component.Message.Id, out var session))
            return;
        if (Context.User.Id != session.UserId)
            return;

        var embed = CardEmbeds.BuildCardEmbed(session.Cards[0], 1, session.Cards.Count);
        var attachments = CardEmbeds.BuildCardAttachments(session.Cards[0]);

        var buttons = new ComponentBuilder()
            .WithButton("Previous", "prev_card", ButtonStyle.Secondary)
            .WithButton("Next", "next_card", ButtonStyle.Secondary)
            .WithButton("💾", "save_card", ButtonStyle.Primary)
            .WithButton("💵", "sell_pack", ButtonStyle.Danger)
            .Build();

        try
        {
            // Replaces the pack cover attachment
            await ModifyOriginalResponseAsync(m =>
            {
                m.Embed = embed;
                m.Components = buttons;
                m.Attachments = new Optional<IEnumerable<FileAttachment>>(attachments);
            });
        }
        finally
        {
            foreach (var attachment in attachments)
                attachment.Dispose();
        }
    }

    [ComponentInteraction("next_card")]
    public Task NextCardAsync() => MoveCardIndexAsync(1);

    [ComponentInteraction("prev_card")]
    public Task PreviousCardAsync() => MoveCardIndexAsync(-1);

    /// <summary>
    ///     Handles the "Save" button click for a card in the pack opening session.
    /// </summary>
    [ComponentInteraction("save_card")]
    public async Task SaveCardAsync()
    {
        await DeferAsync(ephemeral: true);

        if (!sessions.Packs.TryGetValue(Component.Message.Id, out var session))
            return;
        if (Context.User.Id != session.UserId)
            return;

        var disabledButtons = new ComponentBuilder()
            .WithButton("Previous", "prev_card", ButtonStyle.Secondary)
            .WithButton("Next", "next_card", ButtonStyle.Secondary)
            .WithButton("💾", "save_card", ButtonStyle.Success, disabled: true)
            .WithButton("💵", "sell_pack", ButtonStyle.Danger)
            .Build();

        Card cardToSave = session.Cards[session.CurrentIndex];
        var cardIdentifier = $"{cardToSave.Name}_{cardToSave.Rarity}";

        UserCardCollection collection = await users.LoadUserCardsAsync(session.UserId);

        if (session.SavedCardIdentifiers.Contains(cardIdentifier))
        {
            await ModifyOriginalResponseAsync(m => m.Components = disabledButtons);
            return;
        }

        session.SavedCardIdentifiers.Add(cardIdentifier);
        await stateStore.SaveAsync();

        if (collection.Cards.Count >= 10)
        {
            await FollowupAsync("❌ Your inventory is full (Max 10 cards)! Delete some cards in `/inventory` first.", ephemeral: true);
            return;
        }

        collection.Cards.Add(cardToSave);
        await users.SaveUserCardsAsync(collection);

        await ModifyOriginalResponseAsync(m => m.Components = disabledButtons);
    }

    /// <summary>
    ///     Handles the "Sell Pack" button click to bulk sell unsaved cards left in the pack.
    /// </summary>
    [ComponentInteraction("sell_pack")]
    public async Task SellPackAsync()
    {
        await DeferAsync(ephemeral: true);

        if (!sessions.Packs.TryGetValue(Component.Message.Id, out var session))
            return;
        if (Context.User.Id != session.UserId)
            return;

        // End the session and persist that before paying out, so neither a double click nor a
        // restart (which restores open sessions) can sell the same pack twice
        if (!sessions.Packs.TryRemove(Component.Message.Id, out _))
            return;
        await stateStore.SaveAsync();

        decimal totalEarned = 0;
        int cardsSold = 0;

        foreach (var card in session.Cards)
        {
            var identifier = $"{card.Name}_{card.Rarity}";
            if (!session.SavedCardIdentifiers.Contains(identifier))
            {
                decimal marketPrice = Money.FromDouble(
                    card.Pricing?.TcgPlayer?.Market ??
                    card.Pricing?.TcgPlayer?.Low ??
                    card.Pricing?.Cardmarket?.Avg ??
                    0.50);

                totalEarned += marketPrice;
                cardsSold++;
            }
        }

        UserCardCollection collection = await users.LoadUserCardsAsync(session.UserId);
        collection.Balance += totalEarned;
        await users.SaveUserCardsAsync(collection);

        logger.LogInformation("User {Username} sold {CardsSold} pack cards for {TotalEarned:F2}, new balance {Balance:F2}",
            Context.User.Username, cardsSold, totalEarned, collection.Balance);

        var buttons = new ComponentBuilder()
            .WithButton("Pack Sold", "disabled_sell", ButtonStyle.Secondary, disabled: true)
            .Build();

        await ModifyOriginalResponseAsync(m =>
        {
            m.Components = buttons;
        });

        await FollowupAsync($"💵 You sold {cardsSold} unsaved cards for **${totalEarned:F2}**!\nYour new balance is **${collection.Balance:F2}**.", ephemeral: true);
    }

    /// <summary>
    ///     Handles the "Previous" and "Next" button clicks for navigating through cards in the pack
    ///     opening session.
    /// </summary>
    /// <param name="direction">
    ///     The direction to move in the card list: -1 for "Previous" and +1 for "Next".
    /// </param>
    private async Task MoveCardIndexAsync(int direction)
    {
        await DeferAsync(ephemeral: true);

        if (!sessions.Packs.TryGetValue(Component.Message.Id, out var session)) return;

        session.CurrentIndex = (session.CurrentIndex + direction + session.Cards.Count) % session.Cards.Count;

        var currentCard = session.Cards[session.CurrentIndex];
        bool isSaved = session.SavedCardIdentifiers.Contains($"{currentCard.Name}_{currentCard.Rarity}");

        var buttons = new ComponentBuilder()
            .WithButton("Previous", "prev_card", ButtonStyle.Secondary)
            .WithButton("Next", "next_card", ButtonStyle.Secondary)
            .WithButton("💾", "save_card",
                        isSaved ? ButtonStyle.Success : ButtonStyle.Primary,
                        disabled: isSaved)
            .WithButton("💵", "sell_pack", ButtonStyle.Danger)
            .Build();

        var embed = CardEmbeds.BuildCardEmbed(currentCard, session.CurrentIndex + 1, session.Cards.Count);
        var attachments = CardEmbeds.BuildCardAttachments(currentCard);

        try
        {
            await ModifyOriginalResponseAsync(m =>
            {
                m.Embed = embed;
                m.Components = buttons;
                m.Attachments = new Optional<IEnumerable<FileAttachment>>(attachments);
            });
        }
        finally
        {
            foreach (var attachment in attachments)
                attachment.Dispose();
        }
    }

    /// <summary>
    ///     Checks whether the user can afford a pack and tells them if not.
    /// </summary>
    /// <param name="packCost">
    ///     The price of the pack.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if the user's balance covers the pack.
    /// </returns>
    private async Task<bool> HasEnoughBalanceAsync(decimal packCost)
    {
        var userCollection = await users.LoadUserCardsAsync(Context.User.Id);
        if (userCollection.Balance >= packCost)
            return true;

        await SendInsufficientBalanceAsync(packCost, userCollection.Balance);
        return false;
    }

    /// <summary>
    ///     Tells the user their balance is too low for the pack.
    /// </summary>
    /// <param name="packCost">
    ///     The price of the pack.
    /// </param>
    /// <param name="balance">
    ///     The user's current balance.
    /// </param>
    private Task SendInsufficientBalanceAsync(decimal packCost, decimal balance)

    {
        return FollowupAsync(
            $"❌ **Insufficient balance!**\n\n" +
            $"Pack cost: **{packCost:F2} EUR**\n" +
            $"Your balance: **{balance:F2} EUR**\n" +
            $"Missing: **{(packCost - balance):F2} EUR**\n\n" +
            $"💡 Sell some cards to earn more credits!",
            ephemeral: true);
    }
}
