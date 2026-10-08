using DiscordBot.Core;
using DiscordBot.Models;

namespace DiscordBot.Tests;

public class PackBuilderTests
{
    private static Card MakeCard(string name, string rarity) => new() { Name = name, Rarity = rarity };

    private static PackSlot MakeSlot(string name, int count, params (string Rarity, double Chance)[] rarities) => new()
    {
        Name = name,
        Count = count,
        Rarities = rarities.ToDictionary(r => r.Rarity, r => r.Chance),
    };

    private static PackProfile MakeProfile(params PackSlot[] slots) => new()
    {
        CardsPerPack = slots.Sum(s => s.Count),
        Slots = [.. slots],
    };

    private static readonly List<Card> MixedSet =
    [
        MakeCard("c1", "Common"),
        MakeCard("c2", "Common"),
        MakeCard("u1", "Uncommon"),
        MakeCard("r1", "Rare"),
        MakeCard("h1", "Holo Rare"),
    ];

    [Fact]
    public void BuildPack_FillsEverySlotInOrder()
    {
        var profile = MakeProfile(
            MakeSlot("Common", 3, ("Common", 100)),
            MakeSlot("Uncommon", 1, ("Uncommon", 100)),
            MakeSlot("Rare", 1, ("Rare", 50), ("Holo Rare", 50)));

        var pack = PackBuilder.BuildPack(profile, MixedSet, new Random(1));

        Assert.Equal(5, pack.Count);
        Assert.All(pack.Take(3), c => Assert.Equal("Common", c.Rarity));
        Assert.Equal("Uncommon", pack[3].Rarity);
        Assert.Contains(pack[4].Rarity, new[] { "Rare", "Holo Rare" });
    }

    [Fact]
    public void BuildPack_RollsOnlyRaritiesTheSetHas()
    {
        var profile = MakeProfile(MakeSlot("Rare", 200, ("Rare", 1), ("Secret Rare", 99)));

        var pack = PackBuilder.BuildPack(profile, MixedSet, new Random(2));

        Assert.All(pack, c => Assert.Equal("Rare", c.Rarity));
    }

    [Fact]
    public void BuildPack_UsesAnyCardWhenNoSlotRarityExists()
    {
        var profile = MakeProfile(MakeSlot("Crown", 50, ("Crown", 100)));

        var pack = PackBuilder.BuildPack(profile, MixedSet, new Random(3));

        Assert.Equal(50, pack.Count);
        Assert.All(pack, c => Assert.Contains(c, MixedSet));
    }

    [Fact]
    public void BuildPack_AnyRarityDrawsFromTheWholeSet()
    {
        var profile = MakeProfile(MakeSlot("Any", 500, (PackSlot.AnyRarity, 100)));

        var pack = PackBuilder.BuildPack(profile, MixedSet, new Random(4));

        Assert.Equal(MixedSet.Select(c => c.Rarity).Distinct().Order(), pack.Select(c => c.Rarity).Distinct().Order());
    }

    [Fact]
    public void BuildPack_MatchesRaritiesCaseInsensitively()
    {
        var profile = MakeProfile(MakeSlot("Rare", 20, ("holo rare", 100)));

        var pack = PackBuilder.BuildPack(profile, MixedSet, new Random(5));

        Assert.All(pack, c => Assert.Equal("Holo Rare", c.Rarity));
    }

    [Fact]
    public void BuildPack_FollowsSlotOdds()
    {
        var profile = MakeProfile(MakeSlot("Rare", 20_000, ("Rare", 75), ("Holo Rare", 25)));

        var pack = PackBuilder.BuildPack(profile, MixedSet, new Random(6));

        double holoShare = pack.Count(c => c.Rarity == "Holo Rare") / (double)pack.Count;
        Assert.InRange(holoShare, 0.23, 0.27);
    }

    [Fact]
    public void BuildPack_ReweightsOddsOverAvailableRarities()
    {
        var profile = MakeProfile(MakeSlot("Rare", 20_000, ("Rare", 60), ("Holo Rare", 20), ("Secret Rare", 20)));

        var pack = PackBuilder.BuildPack(profile, MixedSet, new Random(7));

        double holoShare = pack.Count(c => c.Rarity == "Holo Rare") / (double)pack.Count;
        Assert.InRange(holoShare, 0.23, 0.27);
    }

    [Fact]
    public void GetUncoveredRarities_ListsRaritiesNoSlotCanRoll()
    {
        var profile = MakeProfile(
            MakeSlot("Common", 1, ("common", 100)),
            MakeSlot("Rare", 1, ("Rare", 100)));

        var uncovered = PackBuilder.GetUncoveredRarities(profile, MixedSet);

        Assert.Equal(new[] { "Holo Rare", "Uncommon" }, uncovered.Order());
    }

    [Fact]
    public void GetUncoveredRarities_IsEmptyWhenASlotTakesAnyRarity()
    {
        var profile = MakeProfile(
            MakeSlot("Common", 1, ("Common", 100)),
            MakeSlot("Any", 1, (PackSlot.AnyRarity, 100)));

        Assert.Empty(PackBuilder.GetUncoveredRarities(profile, MixedSet));
    }
}
