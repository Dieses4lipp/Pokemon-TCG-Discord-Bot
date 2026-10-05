using Discord;
using Discord.Interactions;
using DiscordBot.Core;

namespace DiscordBot.Modules.Expedition;

/// <summary>
///     Suggests card names from the invoking user's own (unlocked) /inventory, supporting the
///     comma-separated multi-card input by only matching/completing the segment currently
///     being typed.
/// </summary>
public sealed class ExpeditionCardsAutocompleteHandler(UserRepository users) : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context, IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        var rawInput = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";

        // Everything up to the last comma is already-picked cards and stays untouched; only the
        // segment after it is what the user is currently typing/completing.
        var lastCommaIndex = rawInput.LastIndexOf(',');
        var prefix = lastCommaIndex >= 0 ? rawInput[..(lastCommaIndex + 1)] + " " : "";
        var currentTyped = (lastCommaIndex >= 0 ? rawInput[(lastCommaIndex + 1)..] : rawInput).Trim();

        var collection = await users.LoadUserCardsAsync(context.User.Id);

        var suggestions = collection.Cards
            .Where(c => !c.IsLocked && c.Name.Contains(currentTyped, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .Take(25)
            .Select(name => new AutocompleteResult(name, $"{prefix}{name}"));

        return AutocompletionResult.FromSuccess(suggestions);
    }
}
