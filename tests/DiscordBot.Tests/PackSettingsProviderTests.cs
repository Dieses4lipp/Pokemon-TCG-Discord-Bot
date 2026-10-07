using DiscordBot.Core;
using DiscordBot.Models;

namespace DiscordBot.Tests;

public class PackSettingsProviderTests
{
    private const string ValidJson = """
        {
          "defaultProfile": "mixed",
          "profiles": {
            "mixed": {
              "cardsPerPack": 2,
              "packPrice": 5.0,
              "slots": [ { "name": "Any", "count": 2, "rarities": { "*": 100 } } ]
            },
            "pocket": {
              "cardsPerPack": 3,
              "packPrice": 3.0,
              "slots": [
                { "name": "Cards 1-2", "count": 2, "rarities": { "One Diamond": 100 } },
                { "name": "Card 3", "count": 1, "rarities": { "Two Diamond": 90.5, "Crown": 9.5 } }
              ]
            }
          },
          "sets": {
            "a1": { "profile": "pocket", "covers": [ "a1_1.jpg" ] }
          }
        }
        """;

    private static PackSettings ValidSettings() => PackSettingsProvider.Parse(ValidJson, "test");

    [Fact]
    public void ShippedPackSettingsAreValid()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Data", "packSettings.json");

        var settings = PackSettingsProvider.Parse(File.ReadAllText(path), path);

        Assert.NotEmpty(settings.Sets);
    }

    [Fact]
    public void Parse_LooksUpSetIdsCaseInsensitivelyAndKeepsTheFolderName()
    {
        var settings = ValidSettings();

        Assert.True(settings.Sets.TryGetValue("A1", out var set));
        Assert.Equal("pocket", set.Profile);
        Assert.Equal("a1", set.Folder);
    }

    [Fact]
    public void Parse_RejectsSetIdsThatOnlyDifferInCase()
    {
        string json = ValidJson.Replace(
            "\"a1\": { \"profile\": \"pocket\", \"covers\": [ \"a1_1.jpg\" ] }",
            "\"a1\": { \"profile\": \"pocket\" }, \"A1\": { \"profile\": \"pocket\" }");

        var ex = Assert.Throws<InvalidDataException>(() => PackSettingsProvider.Parse(json, "test"));
        Assert.Contains("configured more than once", ex.Message);
    }

    [Fact]
    public void Validate_AcceptsValidSettings()
    {
        Assert.Empty(PackSettingsProvider.Validate(ValidSettings()));
    }

    [Fact]
    public void Validate_RejectsSlotCountsThatDoNotAddUpToCardsPerPack()
    {
        var settings = ValidSettings();
        settings.Profiles["pocket"].CardsPerPack = 5;

        Assert.Contains(PackSettingsProvider.Validate(settings), e => e.Contains("slots add up to 3"));
    }

    [Fact]
    public void Validate_RejectsChancesThatDoNotAddUpTo100()
    {
        var settings = ValidSettings();
        settings.Profiles["pocket"].Slots[1].Rarities["Crown"] = 5;

        Assert.Contains(PackSettingsProvider.Validate(settings), e => e.Contains("slot 'Card 3'") && e.Contains("expected 100%"));
    }

    [Fact]
    public void Validate_RejectsNonPositiveChances()
    {
        var settings = ValidSettings();
        settings.Profiles["pocket"].Slots[1].Rarities["Two Diamond"] = 100;
        settings.Profiles["pocket"].Slots[1].Rarities["Crown"] = 0;

        Assert.Contains(PackSettingsProvider.Validate(settings), e => e.Contains("must be positive"));
    }

    [Fact]
    public void Validate_RejectsUnknownProfiles()
    {
        var settings = ValidSettings();
        settings.DefaultProfile = "missing";
        settings.Sets["a1"].Profile = "also-missing";

        var errors = PackSettingsProvider.Validate(settings);

        Assert.Contains(errors, e => e.Contains("defaultProfile 'missing'"));
        Assert.Contains(errors, e => e.Contains("unknown profile 'also-missing'"));
    }

    [Fact]
    public void Validate_RejectsProfilesWithoutSlotsAndNegativePrices()
    {
        var settings = ValidSettings();
        settings.Profiles["mixed"].Slots.Clear();
        settings.Profiles["mixed"].CardsPerPack = 0;
        settings.Profiles["mixed"].PackPrice = -1;

        var errors = PackSettingsProvider.Validate(settings);

        Assert.Contains(errors, e => e.Contains("has no slots"));
        Assert.Contains(errors, e => e.Contains("must not be negative"));
    }
}
