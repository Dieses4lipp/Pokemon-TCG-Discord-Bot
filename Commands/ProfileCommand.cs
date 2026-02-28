using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Services;

namespace DiscordBot.Commands;

/// <summary>
///     Provides a command to display a user's profile with card statistics.
/// </summary>
public class ProfileCommand : ModuleBase<SocketCommandContext>
{
    /// <summary>
    ///     Displays a user's profile, including stats like packs pulled, cards saved, and favorite card.
    /// </summary>
    [Command("profile")]
    public async Task ProfileAsync(SocketUser user)
    {
        if (!CommandHandler.BotActive)
        {
            await ReplyAsync("💤 Bot is currently inactive. Use '!turnon' to activate the bot.");
            return;
        }
        var collection = await CardStorage.LoadUserCardsAsync(user.Id);
        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle($"{user.Username}'s Profile")
            .AddField("Packs Pulled", collection.PacksPulled)
            .AddField("Cards Saved", collection.Cards.Count)
            .AddField("Different Cards Saved", collection.DifferentCardsSaved)
            .AddField("Cards Traded", collection.CardsTraded)
            .WithColor(Color.Blue);
        if (collection.FavoriteCard != null)
        {
            embed.AddField("⭐Favorite Card⭐", collection.FavoriteCard.Name).WithImageUrl($"{collection.FavoriteCard.Image}/low.png");
        }
        else
        {
            embed.AddField("Favorite Card", "None");
        }

        await ReplyAsync(embed: embed.Build());
    }
}