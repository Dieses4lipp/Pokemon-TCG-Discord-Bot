using Discord;
using Discord.WebSocket;
using DiscordBot.Core;

namespace DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.ProfileCommand;

/// <summary>
///     A class that contains the Handler for the /profile command
/// </summary>
public static class ProfileCommandHandler
{
    /// <summary>
    ///     Handles a slash command to display a user's trainer profile, including statistics and
    ///     favorite card information.
    /// </summary>
    /// <param name="command">
    ///     The slash command that triggered the handler, containing user data and options.
    /// </param>
    public static async Task Handle(SocketSlashCommand command)
    {
        // Defer the response to give more time to process
        await command.DeferAsync(ephemeral: true);

        if (!CommandHandler.BotActive)
        {
            await command.FollowupAsync("💤 Bot is currently inactive. Use '/turnon' to activate the bot.");
            return;
        }

        if (command.Data.Options.First(x => x.Name == "user").Value is not SocketUser user)
        {
            await command.FollowupAsync("User could not be found");
            return;
        }

        var collection = await CardStorage.LoadUserCardsAsync(user.Id);

        var embed = new EmbedBuilder()
            .WithTitle($"📊 {user.Username}'s Trainer Profile")
            .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
            .AddField("📦 Packs Pulled", collection.PacksPulled, true)
            .AddField("📇 Total Saved", collection.Cards?.Count ?? 0, true)
            .AddField("✨ Unique Cards", collection.DifferentCardsSaved, true)
            .AddField("🤝 Cards Traded", collection.CardsTraded, true)
            .WithColor(Color.Blue)
            .WithCurrentTimestamp();

        if (collection.FavoriteCard != null)
        {
            embed.AddField("⭐Favorite Card⭐", collection.FavoriteCard.Name)
                .WithImageUrl($"{collection.FavoriteCard.Image}/low.png");
        }
        else
        {
            embed.AddField("⭐ Favorite Card", "No favorite set yet! Use ⭐ in your inventory.");
        }

        await command.FollowupAsync(embed: embed.Build());
    }
}