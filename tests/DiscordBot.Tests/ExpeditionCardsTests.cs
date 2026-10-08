using DiscordBot.Core;
using DiscordBot.Models;

namespace DiscordBot.Tests;

public class ExpeditionCardsTests
{
    private static Card MakeCard(string name, string rarity = "Rare", bool locked = false) =>
        new() { Name = name, Rarity = rarity, IsLocked = locked };

    [Fact]
    public void TrySelect_PicksOneDistinctCopyPerName()
    {
        List<Card> cards = [MakeCard("Pikachu"), MakeCard("Pikachu"), MakeCard("Eevee")];

        bool ok = ExpeditionCards.TrySelect(cards, ["pikachu", "Pikachu", "Eevee"], out var selected, out var missing);

        Assert.True(ok);
        Assert.Null(missing);
        Assert.Equal(3, selected.Count);
        Assert.Same(cards[0], selected[0]);
        Assert.Same(cards[1], selected[1]);
        Assert.Same(cards[2], selected[2]);
    }

    [Fact]
    public void TrySelect_FailsWhenThereAreNotEnoughCopies()
    {
        List<Card> cards = [MakeCard("Pikachu")];

        bool ok = ExpeditionCards.TrySelect(cards, ["Pikachu", "Pikachu"], out var selected, out var missing);

        Assert.False(ok);
        Assert.Equal("Pikachu", missing);
        Assert.Empty(selected);
    }

    [Fact]
    public void TrySelect_SkipsLockedCards()
    {
        List<Card> cards = [MakeCard("Pikachu", locked: true), MakeCard("Pikachu")];

        ExpeditionCards.TrySelect(cards, ["Pikachu"], out var selected, out _);
        Assert.Same(cards[1], Assert.Single(selected));

        cards[1].IsLocked = true;
        Assert.False(ExpeditionCards.TrySelect(cards, ["Pikachu"], out _, out _));
    }

    [Fact]
    public void Unlock_UnlocksOneLockedCopyPerSentCard()
    {
        List<Card> cards =
        [
            MakeCard("Pikachu", locked: true),
            MakeCard("Pikachu", locked: true),
            MakeCard("Pikachu", locked: true),
            MakeCard("Pikachu"),
        ];

        int unlocked = ExpeditionCards.Unlock(cards, [MakeCard("Pikachu"), MakeCard("Pikachu")]);

        Assert.Equal(2, unlocked);
        Assert.Equal(1, cards.Count(c => c.IsLocked));
    }

    [Fact]
    public void Unlock_MatchesNameAndRarityAndSkipsCardsNoLongerOwned()
    {
        List<Card> cards = [MakeCard("Pikachu", "Common", locked: true), MakeCard("Eevee", locked: true)];

        int unlocked = ExpeditionCards.Unlock(cards, [MakeCard("Pikachu", "Holo Rare"), MakeCard("Eevee"), MakeCard("Mew")]);

        Assert.Equal(1, unlocked);
        Assert.True(cards[0].IsLocked);
        Assert.False(cards[1].IsLocked);
    }

    [Fact]
    public void SelectThenUnlock_RestoresTheCollection()
    {
        List<Card> cards = [MakeCard("Pikachu"), MakeCard("Eevee"), MakeCard("Eevee")];
        ExpeditionCards.TrySelect(cards, ["Eevee", "Pikachu"], out var selected, out _);
        foreach (var card in selected) card.IsLocked = true;

        var sentCopies = selected.Select(c => c with { }).ToList();
        ExpeditionCards.Unlock(cards, sentCopies);

        Assert.All(cards, c => Assert.False(c.IsLocked));
    }
}
