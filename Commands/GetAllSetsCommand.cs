using Discord;
using Discord.Commands;
using DiscordBot.Core;
using DiscordBot.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DiscordBot.Commands
{
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
                await ReplyAsync("The bot is currently inactive and not responding to commands.");
                return;
            }

            try
            {
                string response = await CommandHandler._httpClient.GetStringAsync(CommandHandler.SetsApiUrl);
                var token = JToken.Parse(response);

                // Attempt to find sets array in response (common keys)
                JArray? setsArray = token.Type == JTokenType.Array ? (JArray)token : null;
                if (setsArray == null)
                {
                    var candidates = new[] { "data", "sets", "results", "items" };
                    foreach (var name in candidates)
                    {
                        var candidate = token[name];
                        if (candidate != null && candidate.Type == JTokenType.Array)
                        {
                            setsArray = (JArray)candidate;
                            break;
                        }
                    }
                }

                if (setsArray == null || !setsArray.Any())
                {
                    await ReplyAsync("No sets found!");
                    return;
                }

                var setList = setsArray
                    .Select(s => new Set
                    {
                        Name = s.Value<string>("name") ?? s.Value<string>("title") ?? "Unknown",
                        Id = s.Value<string>("id") ?? s.Value<string>("code") ?? s.Value<string>("setId") ?? "unknown",
                        ImageUrl = s.Value<string>("images")?.ToString() ?? s.Value<string>("imageUrl") ?? string.Empty
                    })
                    .Take(100)
                    .ToList();

                var selectMenu = new SelectMenuBuilder()
                    .WithPlaceholder("Select a set")
                    .WithCustomId("set_selection")
                    .WithMinValues(1)
                    .WithMaxValues(1);

                foreach (var set in setList.Take(25))
                    selectMenu.AddOption(set.Name, set.Id, $"ID: {set.Id}");

                var component = new ComponentBuilder().WithSelectMenu(selectMenu);
                var embed = new EmbedBuilder()
                    .WithTitle("Pokémon Sets")
                    .WithDescription("Shown sets (for information only)")
                    .WithColor(Color.Green)
                    .Build();

                await ReplyAsync(embed: embed, components: component.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync($"An error occurred while fetching sets: {ex.Message}");
            }
        }
    }
}