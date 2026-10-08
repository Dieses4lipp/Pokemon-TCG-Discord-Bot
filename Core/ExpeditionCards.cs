using DiscordBot.Models;

namespace DiscordBot.Core;

/// <summary>
///     Picks the cards sent on an expedition and unlocks them again when it is claimed.
/// </summary>
public static class ExpeditionCards
{
    /// <summary>
    ///     Resolves each requested name to a distinct, unlocked card of the collection. Cards are
    ///     tracked by position, not value equality, so identical copies can each be picked once.
    /// </summary>
    /// <param name="cards">
    ///     The cards of the collection.
    /// </param>
    /// <param name="requestedNames">
    ///     The card names to send, matched case-insensitively; a name listed twice needs two copies.
    /// </param>
    /// <param name="selected">
    ///     The picked cards, in request order; empty if the selection failed.
    /// </param>
    /// <param name="missingName">
    ///     The first name without an available card, if the selection failed.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if every name resolved to a card.
    /// </returns>
    public static bool TrySelect(
        List<Card> cards,
        IEnumerable<string> requestedNames,
        out List<Card> selected,
        out string? missingName)
    {
        selected = [];
        missingName = null;
        var pickedIndices = new HashSet<int>();

        foreach (var name in requestedNames)
        {
            int matchIndex = FindNext(cards, pickedIndices, c =>
                c.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && !c.IsLocked);

            if (matchIndex == -1)
            {
                selected = [];
                missingName = name;
                return false;
            }

            pickedIndices.Add(matchIndex);
            selected.Add(cards[matchIndex]);
        }

        return true;
    }

    /// <summary>
    ///     Unlocks one locked copy per sent card, matched by name and rarity. Sent cards that are no
    ///     longer in the collection are skipped.
    /// </summary>
    /// <param name="cards">
    ///     The cards of the collection.
    /// </param>
    /// <param name="sentCards">
    ///     The cards that were sent on the expedition.
    /// </param>
    /// <returns>
    ///     The number of cards unlocked.
    /// </returns>
    public static int Unlock(List<Card> cards, IEnumerable<Card> sentCards)
    {
        var unlockedIndices = new HashSet<int>();

        foreach (var sentCard in sentCards)
        {
            int matchIndex = FindNext(cards, unlockedIndices, c =>
                c.IsLocked && c.Name == sentCard.Name && c.Rarity == sentCard.Rarity);

            if (matchIndex == -1) continue;

            cards[matchIndex].IsLocked = false;
            unlockedIndices.Add(matchIndex);
        }

        return unlockedIndices.Count;
    }

    private static int FindNext(List<Card> cards, HashSet<int> usedIndices, Predicate<Card> match)
    {
        int index = cards.FindIndex(match);
        while (index != -1 && usedIndices.Contains(index))
            index = cards.FindIndex(index + 1, match);
        return index;
    }
}
