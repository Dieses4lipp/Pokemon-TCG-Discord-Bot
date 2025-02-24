using Discord;
using Discord.Commands;
using DiscordBot.Core;
using DiscordBot.Models;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordBot.Commands
{
    public class GetAllSetsCommand : ModuleBase<SocketCommandContext>
    {
        [Command("sets")]
        public async Task GetAllSetsAsync()
        {
            if (!CommandHandler.BotActive)
            {
                await ReplyAsync("The bot is currently inactive and not responding to commands.");
                return;
            }
            try
            {
                string response = await CommandHandler._httpClient.GetStringAsync(CommandHandler.SetsApiUrl);
                var setData = JsonConvert.DeserializeObject<SetApiResponse>(response);

                if (setData?.Data == null || setData.Data.Count == 0)
                {
                    await ReplyAsync("No sets found!");
                    return;
                }

                SelectMenuBuilder? selectMenu = new SelectMenuBuilder()
                    .WithPlaceholder("Select a set")
                    .WithCustomId("set_selection")
                    .WithMinValues(1)
                    .WithMaxValues(1);

                foreach (Set set in setData.Data.Take(25))
                {
                    selectMenu.AddOption(set.Name, set.Id, $"ID: {set.Id}");
                }

                ComponentBuilder? component = new ComponentBuilder()
                    .WithSelectMenu(selectMenu);

                Embed? embed = new EmbedBuilder()
                    .WithTitle("Pokémon Karten Sets")
                    .WithDescription("The Sets shown - This is no selection, its just for Information to retrieve the Set IDs")
                    .WithColor(Color.Green)
                    .Build();

                await ReplyAsync(embed: embed, components: component.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync("An error occurred: " + ex.Message);
            }
        }
    }
}
