using Discord;
using Discord.WebSocket;
using DiscordBot.Core;

namespace DiscordBot.Commands.SlashCommandHandlers.ExpeditionCommands;

/// <summary>
///     A class that contains the Handler for autocomplete interactions of the /expedition command.
/// </summary>
public static class ExpeditionAutocompleteHandler
{
    public static async Task Handle(SocketAutocompleteInteraction interaction)
    {
        switch (interaction.Data.Current.Name)
        {
            case "location":
                await HandleLocationAutoComplete(interaction);
                break;

            case "cards":
                await HandleCardsAutoComplete(interaction);
                break;
        }
    }

    private static async Task HandleLocationAutoComplete(SocketAutocompleteInteraction interaction)
    {
        var userInput = interaction.Data.Current.Value?.ToString() ?? "";

        var suggestions = ExpeditionSettingsProvider.Locations
            .Where(l => l.Name.Contains(userInput, StringComparison.OrdinalIgnoreCase))
            .OrderBy(l => l.DurationMinutes)
            .Select(l => new AutocompleteResult($"{l.Name} ({l.DurationMinutes} min)", l.Id))
            .Take(25)
            .ToList();

        await interaction.RespondAsync(suggestions);
    }

    /// <summary>
    ///     Suggests card names from the invoking user's own (unlocked) /inventory, supporting the
    ///     comma-separated multi-card input by only matching/completing the segment currently
    ///     being typed.
    /// </summary>
    private static async Task HandleCardsAutoComplete(SocketAutocompleteInteraction interaction)
    {
        var rawInput = interaction.Data.Current.Value?.ToString() ?? "";

        // Everything up to the last comma is already-picked cards and stays untouched; only the
        // segment after it is what the user is currently typing/completing.
        var lastCommaIndex = rawInput.LastIndexOf(',');
        var prefix = lastCommaIndex >= 0 ? rawInput[..(lastCommaIndex + 1)] + " " : "";
        var currentTyped = (lastCommaIndex >= 0 ? rawInput[(lastCommaIndex + 1)..] : rawInput).Trim();

        var collection = await CardStorage.LoadUserCardsAsync(interaction.User.Id);

        var suggestions = collection.Cards
            .Where(c => !c.IsLocked && c.Name.Contains(currentTyped, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .Take(25)
            .Select(name => new AutocompleteResult(name, $"{prefix}{name}"))
            .ToList();

        await interaction.RespondAsync(suggestions);
    }
}
