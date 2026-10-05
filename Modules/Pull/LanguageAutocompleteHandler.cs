using Discord;
using Discord.Interactions;

namespace DiscordBot.Modules.Pull;

/// <summary>
///     Suggests the card languages the API supports.
/// </summary>
public sealed class LanguageAutocompleteHandler : AutocompleteHandler
{
    private static readonly AutocompleteResult[] AvailableLanguages =
    [
        new("English", "en"),
        new("French", "fr"),
        new("Spanish", "es"),
        new("Italian", "it"),
        new("Portuguese", "pt"),
        new("German", "de"),
    ];

    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context, IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        return Task.FromResult(AutocompletionResult.FromSuccess(AvailableLanguages));
    }
}
