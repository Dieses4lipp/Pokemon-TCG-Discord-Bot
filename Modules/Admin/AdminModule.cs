using Discord;
using Discord.Interactions;
using DiscordBot.Core;
using DiscordBot.Models;
using DiscordBot.Preconditions;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Modules.Admin;

/// <summary>
///     Owner-only commands. Hidden from members without Administrator in the command picker;
///     <see cref="RequireBotOwnerAttribute"/> is what actually guards them.
/// </summary>
[DefaultMemberPermissions(GuildPermission.Administrator)]
[RequireBotOwner]
public sealed class AdminModule(
    BotState botState,
    BotStateStore stateStore,
    UserRepository users,
    CardApiClient api,
    ILogger<AdminModule> logger) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("lockset", "Locks a specific set to prevent it from being pulled. (Admin only)")]
    public async Task LockSetAsync(
        [Summary("set-id", "The Set-ID of the Set you want to lock.")] string setId)
    {
        await DeferAsync(ephemeral: true);

        if (string.IsNullOrWhiteSpace(setId))
        {
            await FollowupAsync("❌ Invalid Set ID.");
            return;
        }

        if (botState.LockSet(setId))
        {
            await stateStore.SaveAsync();
            await FollowupAsync($"🔒 **Set Locked:** Users can no longer pull from `{setId}`.");
        }
        else
        {
            await FollowupAsync($"⚠️ Set `{setId}` is already in the locked list.");
        }
    }

    [SlashCommand("unlockset", "Unlocks a specific set to allow it to be pulled again. (Admin only)")]
    public async Task UnlockSetAsync(
        [Summary("set-id", "The Set-ID of the Set you want to unlock.")] string setId)
    {
        await DeferAsync(ephemeral: true);

        if (string.IsNullOrWhiteSpace(setId))
        {
            await FollowupAsync("❌ Invalid Set ID.");
            return;
        }

        if (botState.UnlockSet(setId))
        {
            await stateStore.SaveAsync();
            await FollowupAsync($"🔓 **Set Unlocked:** Users can now pull from `{setId}` again.");
        }
        else
        {
            await FollowupAsync($"ℹ️ Set `{setId}` wasn't locked to begin with.");
        }
    }

    [SlashCommand("turnoff", "Turns off the bot to prevent it from responding to commands. (Admin only)")]
    public async Task TurnOffAsync()
    {
        await DeferAsync(ephemeral: true);

        botState.IsActive = false;
        await stateStore.SaveAsync();

        await FollowupAsync("💤 Bot is now inactive.");
    }

    [SlashCommand("turnon", "Turns on the bot to allow it to respond to commands. (Admin only)")]
    public async Task TurnOnAsync()
    {
        await DeferAsync(ephemeral: true);

        botState.IsActive = true;
        await stateStore.SaveAsync();

        await FollowupAsync("🔌 Bot is now active.");
    }

    [SlashCommand("addbalance", "Adds money to a user's balance; negative amounts remove money. (Admin only)")]
    public async Task AddBalanceAsync(
        [Summary("user", "The user whose balance to change.")] IUser user,
        [Summary("amount", "The amount to add, negative to remove.")] double amount)
    {
        await DeferAsync(ephemeral: true);

        var collection = await users.LoadUserCardsAsync(user.Id);
        decimal newBalance = collection.Balance + Money.FromDouble(amount);

        if (newBalance < 0)
        {
            await FollowupAsync($"❌ {user.Username} only has 💰 {collection.Balance:F2}, the balance can't go below 0.");
            return;
        }

        await ChangeBalanceAsync(user, collection, newBalance);
    }

    [SlashCommand("setbalance", "Sets a user's balance. (Admin only)")]
    public async Task SetBalanceAsync(
        [Summary("user", "The user whose balance to set.")] IUser user,
        [Summary("amount", "The new balance.")] double amount)
    {
        await DeferAsync(ephemeral: true);

        if (amount < 0)
        {
            await FollowupAsync("❌ The balance can't be negative.");
            return;
        }

        await ChangeBalanceAsync(user, await users.LoadUserCardsAsync(user.Id), Money.FromDouble(amount));
    }

    [SlashCommand("givecard", "Adds a card to a user's collection. (Admin only)")]
    public async Task GiveCardAsync(
        [Summary("user", "The user who gets the card.")] IUser user,
        [Summary("card-id", "The TCGdex card ID, e.g. swsh1-25.")] string cardId)
    {
        await DeferAsync(ephemeral: true);

        var card = (await api.FetchCardDetailsAsync([cardId.Trim()], "en")).FirstOrDefault();
        if (card == null)
        {
            await FollowupAsync($"❌ No card found with ID `{cardId}`.");
            return;
        }

        var collection = await users.LoadUserCardsAsync(user.Id);
        collection.Cards.Add(card);
        await users.SaveUserCardsAsync(collection);

        logger.LogInformation("Admin {AdminId} gave card {CardId} ({CardName}, {Rarity}) to user {UserId}",
            Context.User.Id, cardId, card.Name, card.Rarity, user.Id);
        await FollowupAsync($"🎁 Gave `{card.Name}` ({card.Rarity}) to {user.Mention}.");
    }

    [SlashCommand("removecard", "Removes one copy of a card from a user's collection. (Admin only)")]
    public async Task RemoveCardAsync(
        [Summary("user", "The user who loses the card.")] IUser user,
        [Summary("card-name", "The name of the card to remove.")] string cardName)
    {
        await DeferAsync(ephemeral: true);

        var collection = await users.LoadUserCardsAsync(user.Id);
        var matches = collection.Cards
            .Where(c => c.Name.Equals(cardName.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            await FollowupAsync($"❌ {user.Username} doesn't own a card named `{cardName}`.");
            return;
        }

        // Keep the favorite copy as long as there is another one to remove
        var card = matches
            .Where(c => !c.IsLocked)
            .OrderBy(c => c.InstanceId == collection.FavoriteCardId)
            .FirstOrDefault();
        if (card == null)
        {
            await FollowupAsync($"🔒 Every copy of `{matches[0].Name}` is on an expedition and can't be removed.");
            return;
        }

        collection.Cards.Remove(card);
        await users.SaveUserCardsAsync(collection);

        logger.LogInformation("Admin {AdminId} removed card {CardName} ({Rarity}) from user {UserId}",
            Context.User.Id, card.Name, card.Rarity, user.Id);
        await FollowupAsync($"🗑️ Removed one `{card.Name}` ({card.Rarity}) from {user.Mention}.");
    }

    [SlashCommand("userinfo", "Shows a user's balance, collection and expedition. (Admin only)")]
    public async Task UserInfoAsync(
        [Summary("user", "The user to look up.")] IUser user)
    {
        await DeferAsync(ephemeral: true);

        var collection = await users.LoadUserCardsAsync(user.Id);
        var expedition = collection.ActiveExpedition;

        var embed = new EmbedBuilder()
            .WithTitle($"🔎 {user.Username}")
            .AddField("Balance", $"💰 {collection.Balance:F2}", true)
            .AddField("Cards", $"{collection.Cards.Count} ({collection.DifferentCardsSaved} different, {collection.Cards.Count(c => c.IsLocked)} locked)", true)
            .AddField("Packs pulled", collection.PacksPulled, true)
            .AddField("Cards traded", collection.CardsTraded, true)
            .AddField("Favorite", collection.FavoriteCard == null ? "—" : $"`{collection.FavoriteCard.Name}` ({collection.FavoriteCard.Rarity})", true)
            .AddField("Expedition", expedition == null
                ? "—"
                : $"`{expedition.LocationId}`, {collection.CardsOnExpedition.Count()} card(s), returns <t:{new DateTimeOffset(expedition.EndTimeUtc).ToUnixTimeSeconds()}:R>", true)
            .WithColor(Color.Blue)
            .Build();

        await FollowupAsync(embed: embed);
    }

    [SlashCommand("restart", "Restarts the bot.")]
    public async Task RestartAsync()
    {
        await DeferAsync(ephemeral: true);
        await FollowupAsync("🔄 Restarting...");

        await Task.Delay(1000);
        Program.RestartBot();
    }

    private async Task ChangeBalanceAsync(IUser user, UserCardCollection collection, decimal newBalance)
    {
        decimal oldBalance = collection.Balance;

        collection.Balance = newBalance;
        await users.SaveUserCardsAsync(collection);

        logger.LogInformation("Admin {AdminId} changed balance of user {UserId} from {OldBalance:F2} to {NewBalance:F2}",
            Context.User.Id, user.Id, oldBalance, newBalance);
        await FollowupAsync($"💰 Balance of {user.Mention}: {oldBalance:F2} → **{newBalance:F2}**.");
    }
}
