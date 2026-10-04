using DiscordBot.Models;

namespace DiscordBot.Core;

/// <summary>
///     Builds booster packs from a set's cards according to a <see cref="PackProfile"/>.
/// </summary>
public static class PackBuilder
{
    /// <summary>
    ///     Draws the cards of one pack, slot by slot. For each card the slot's rarity odds are
    ///     re-weighted over the rarities that actually exist in the set, so a missing rarity never
    ///     produces an empty slot; if none of a slot's rarities exist, any card of the set is used.
    /// </summary>
    /// <param name="profile">
    ///     The pack layout to follow.
    /// </param>
    /// <param name="setCards">
    ///     The cards of the set to draw from. Must not be empty.
    /// </param>
    /// <param name="random">
    ///     The random number generator used for all rolls.
    /// </param>
    /// <returns>
    ///     The cards of the pack, in slot order.
    /// </returns>
    public static List<Card> BuildPack(PackProfile profile, List<Card> setCards, Random random)
    {
        var cardsByRarity = setCards
            .GroupBy(c => c.Rarity ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var pack = new List<Card>(profile.CardsPerPack);

        foreach (var slot in profile.Slots)
        {
            // Only rarities present in this set can be rolled
            var available = slot.Rarities
                .Where(r => r.Key == PackSlot.AnyRarity || cardsByRarity.ContainsKey(r.Key))
                .ToList();

            for (int i = 0; i < slot.Count; i++)
            {
                List<Card> pool = setCards;

                if (available.Count > 0)
                {
                    string rarity = RollRarity(available, random);
                    if (rarity != PackSlot.AnyRarity) pool = cardsByRarity[rarity];
                }

                pack.Add(pool[random.Next(pool.Count)]);
            }
        }

        return pack;
    }

    /// <summary>
    ///     Gets the rarities of a set that no slot of the profile can ever roll, to spot rarity
    ///     names in the config that do not match the card API.
    /// </summary>
    /// <param name="profile">
    ///     The pack layout to check.
    /// </param>
    /// <param name="setCards">
    ///     The cards of the set.
    /// </param>
    /// <returns>
    ///     The distinct rarities of the set not covered by the profile.
    /// </returns>
    public static List<string> GetUncoveredRarities(PackProfile profile, List<Card> setCards)
    {
        if (profile.Slots.Any(s => s.Rarities.ContainsKey(PackSlot.AnyRarity)))
            return [];

        var covered = profile.Slots
            .SelectMany(s => s.Rarities.Keys)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return setCards
            .Select(c => c.Rarity ?? string.Empty)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(r => !covered.Contains(r))
            .ToList();
    }

    /// <summary>
    ///     Rolls one rarity, weighted by the given chances (which need not add up to 100).
    /// </summary>
    /// <param name="rarities">
    ///     The rarities with their chances.
    /// </param>
    /// <param name="random">
    ///     The random number generator.
    /// </param>
    /// <returns>
    ///     The rolled rarity.
    /// </returns>
    private static string RollRarity(List<KeyValuePair<string, double>> rarities, Random random)
    {
        double roll = random.NextDouble() * rarities.Sum(r => r.Value);
        double cumulative = 0.0;

        foreach (var rarity in rarities)
        {
            cumulative += rarity.Value;
            if (roll < cumulative) return rarity.Key;
        }

        return rarities[^1].Key;
    }
}
