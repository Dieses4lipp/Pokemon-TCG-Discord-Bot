using Discord;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Models;
using Newtonsoft.Json;

namespace DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.PullCommand;

/// <summary>
///     A class that contains the Handler for an autocomplete interaction of the /pull command
/// </summary>
public static class PullAutocompleteHandler
{
    public static async Task Handle(SocketAutocompleteInteraction interaction)
    {
        // Distinguishes between option field names
        switch (interaction.Data.Current.Name)
        {
            case "set-id":
                await HandleSetAutoComplete(interaction);
                break;

            case "language":
                await HandleLanguageAutoComplete(interaction);
                break;
        }
    }

    /// <summary>
    ///     Handles the auto complete interaction
    /// </summary>
    /// <param name="interaction">
    ///     <inheritdoc cref="SocketAutocompleteInteraction" path="/summary"/>
    /// </param>
    public static async Task HandleSetAutoComplete(SocketAutocompleteInteraction interaction)
    {
        var userInput = interaction.Data.Current.Value?.ToString() ?? "";

        try
        {
            string response = await CommandHandler._httpClient.GetStringAsync(CommandHandler.SetsApiUrl);
            var setsList = JsonConvert.DeserializeObject<List<Set>>(response);

            if (setsList == null)
            {
                Console.WriteLine("Couldn't get Auo fill set-ids");
                return;
            }

            var suggestions = setsList
                .Where(s => s.Name.Contains(userInput, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Name)
                .Select(s => new AutocompleteResult(s.Name, s.Id))
                // Option limit
                .Take(25)
                .ToList();

            await interaction.RespondAsync(suggestions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Autocomplete Error: {ex.Message}");
        }
    }

    public static async Task HandleLanguageAutoComplete(SocketAutocompleteInteraction interaction)
    {
        try
        {
            AutocompleteResult[] availableLanguages = [
                new("English", "en"),
                new("French", "fr"),
                new("Spanish", "es"),
                new("Italian", "it"),
                new("Portuguese", "pt"),
                new("German", "de"),
            ];

            await interaction.RespondAsync(availableLanguages);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Autocomplete Error: {ex.Message}");
        }
    }
}