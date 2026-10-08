using Discord;
using Discord.Interactions;
using DiscordBot.Core;

namespace DiscordBot.Modules.Pull;

/// <summary>
///     Suggests sets whose name contains what the user typed.
/// </summary>
public sealed class SetIdAutocompleteHandler(CardApiClient api) : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context, IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        var userInput = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";

        var setsList = await api.GetSetsAsync();

        var suggestions = setsList
            .Where(s => s.Name.Contains(userInput, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Name)
            .Select(s => new AutocompleteResult(s.Name, s.Id))
            // Option limit
            .Take(25);

        return AutocompletionResult.FromSuccess(suggestions);
    }
}
