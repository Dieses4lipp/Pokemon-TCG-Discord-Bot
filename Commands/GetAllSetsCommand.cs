using Discord;
using Discord.Commands;
using DiscordBot.Core;
using DiscordBot.Models;
using Newtonsoft.Json;

namespace DiscordBot.Commands;

/// <summary>
///     Provides a command to retrieve and display all available Pokémon sets.
/// </summary>
public class GetAllSetsCommand : ModuleBase<SocketCommandContext>
{
    /// <summary>
    ///     Fetches all sets from the API and displays them in a select menu.
    /// </summary>
    [Command("sets", RunMode = RunMode.Async)]
    public async Task GetAllSetsAsync()
    {
        if (!CommandHandler.BotActive)
        {
            await ReplyAsync("💤 Bot is currently inactive. Use '!turnon' to activate the bot.");
            return;
        }
        try
        {
            string response = await CommandHandler._httpClient.GetStringAsync(CommandHandler.SetsApiUrl);
            var setData = JsonConvert.DeserializeObject<List<SetApiResponse>>(response);

            if (setData == null || setData.Count == null)
            {
                await ReplyAsync("❌ No sets found!");
                return;
            }

            SelectMenuBuilder? selectMenu = new SelectMenuBuilder()
                .WithPlaceholder("Select a set")
                .WithCustomId("set_selection")
                .WithMinValues(1)
                .WithMaxValues(1);

            foreach (SetApiResponse set in setData.Take(25))
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
            await ReplyAsync($"⚠️ An error occurred while fetching sets, {ex.Message}");
        }
    }
}