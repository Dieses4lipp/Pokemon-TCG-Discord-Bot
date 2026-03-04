using Discord;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Models;
using Newtonsoft.Json;

namespace DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.PullCommand;

public static class PullAutocompleteHandler
{
    public static async Task Handle(SocketAutocompleteInteraction interaction)
    {
        // Get what the user is typing
        var userInput = interaction.Data.Current.Value?.ToString() ?? "";

        try
        {
            // Fetch your data (using your existing HttpClient)
            string response = await CommandHandler._httpClient.GetStringAsync(CommandHandler.SetsApiUrl + "?pagination:itemsPerPage=50");
            var setsList = JsonConvert.DeserializeObject<List<Set>>(response);

            if (setsList == null)
            {
                Console.WriteLine("Couldn't get Auo fill set-ids");
                return;
            }

            // Filter and create suggestions
            var suggestions = setsList
                .Where(s => s.Name.Contains(userInput, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Name)
                .Select(s => new AutocompleteResult(s.Name, s.Id))
                .Take(25)
                .ToList();

            // Respond to the interaction with the suggestions
            await interaction.RespondAsync(suggestions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Autocomplete Error: {ex.Message}");
        }
    }
}