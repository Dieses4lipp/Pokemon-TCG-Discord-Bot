using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Services;
using System.Threading.Tasks;

namespace DiscordBot.Commands
{
    public class ProfileCommand : ModuleBase<SocketCommandContext>
    {
        [Command("profile")]
        public async Task ProfileAsync(SocketUser user)
        {
            if (!CommandHandler.BotActive)
            {
                await ReplyAsync("The bot is currently inactive and not responding to commands.");
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
                embed.AddField("Favorite Card", collection.FavoriteCard.Name).WithImageUrl(collection.FavoriteCard.Images.Small);
            }
            else
            {
                embed.AddField("Favorite Card", "None");
            }

            await ReplyAsync(embed: embed.Build());
        }
    }
}
