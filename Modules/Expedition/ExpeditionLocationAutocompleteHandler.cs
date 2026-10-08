using Discord;
using Discord.Interactions;
using DiscordBot.Core;

namespace DiscordBot.Modules.Expedition;

/// <summary>
///     Suggests the configured expedition locations, shortest first.
/// </summary>
public sealed class ExpeditionLocationAutocompleteHandler : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context, IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        var userInput = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";

        var suggestions = ExpeditionSettingsProvider.Locations
            .Where(l => l.Name.Contains(userInput, StringComparison.OrdinalIgnoreCase))
            .OrderBy(l => l.DurationMinutes)
            .Select(l => new AutocompleteResult($"{l.Name} ({l.DurationMinutes} min)", l.Id))
            .Take(25);

        return Task.FromResult(AutocompletionResult.FromSuccess(suggestions));
    }
}
