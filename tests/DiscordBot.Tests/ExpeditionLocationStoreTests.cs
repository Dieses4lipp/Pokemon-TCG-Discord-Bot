using DiscordBot.Core;
using DiscordBot.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace DiscordBot.Tests;

public sealed class ExpeditionLocationStoreTests : IAsyncLifetime
{
    private TestDatabase _db = default!;
    private string _seedFile = default!;

    public async Task InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync();
        _seedFile = Path.Combine(_db.Directory, "expeditionSettings.json");
        WriteSeed(("forest", 15), ("cave", 60));
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private void WriteSeed(params (string Id, int Minutes)[] locations) =>
        File.WriteAllText(_seedFile, Newtonsoft.Json.JsonConvert.SerializeObject(new ExpeditionSettings
        {
            Locations = locations.Select(l => new ExpeditionLocation
            {
                Id = l.Id,
                Name = l.Id.ToUpperInvariant(),
                DurationMinutes = l.Minutes,
                MinReward = 1,
                MaxReward = 2,
                CardRewardChance = 0.5,
                CardRewardCount = 1,
            }).ToList(),
        }));

    private ExpeditionLocationStore CreateStore() =>
        new(_db.Database, _seedFile, NullLogger<ExpeditionLocationStore>.Instance);

    [Fact]
    public async Task Load_FillsAnEmptyTableFromTheFile()
    {
        var store = CreateStore();
        await store.LoadAsync();

        Assert.Equal(["cave", "forest"], store.Locations.Select(l => l.Id));
        Assert.Equal(15, store.GetLocationById("FOREST")?.DurationMinutes);
    }

    [Fact]
    public async Task Edits_SurviveARestartAndIgnoreTheFile()
    {
        var store = CreateStore();
        await store.LoadAsync();
        var edited = store.GetLocationById("forest")!;
        edited.DurationMinutes = 5;
        await store.SaveAsync(edited);

        WriteSeed(("other", 1));
        var restarted = CreateStore();
        await restarted.LoadAsync();

        Assert.Equal(5, restarted.GetLocationById("forest")?.DurationMinutes);
        Assert.Null(restarted.GetLocationById("other"));
    }

    [Fact]
    public async Task Reload_ResetsToTheFile()
    {
        var store = CreateStore();
        await store.LoadAsync();
        WriteSeed(("forest", 30));

        Assert.Equal(1, await store.ReloadFromFileAsync());

        Assert.Equal(30, Assert.Single(store.Locations).DurationMinutes);
    }

    [Fact]
    public async Task Save_RejectsInvalidLocationsAndKeepsTheOldOne()
    {
        var store = CreateStore();
        await store.LoadAsync();
        var edited = store.GetLocationById("forest")!;
        edited.MaxReward = 0;

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(edited));

        await store.LoadAsync();
        Assert.Equal(2, store.GetLocationById("forest")?.MaxReward);
    }

    [Fact]
    public async Task Load_RejectsDuplicateIdsInTheFile()
    {
        WriteSeed(("forest", 15), ("Forest", 20));

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateStore().LoadAsync());
    }

    [Fact]
    public async Task ShippedSettingsFile_IsValid()
    {
        var store = new ExpeditionLocationStore(_db.Database, ExpeditionLocationStore.DefaultSeedFilePath,
            NullLogger<ExpeditionLocationStore>.Instance);

        await store.LoadAsync();

        Assert.NotEmpty(store.Locations);
    }

    [Theory]
    [InlineData(0, 1, 2, 0.5, 1)]
    [InlineData(10, -1, 2, 0.5, 1)]
    [InlineData(10, 3, 2, 0.5, 1)]
    [InlineData(10, 1, 2, 1.5, 1)]
    [InlineData(10, 1, 2, 0.5, 0)]
    public void Rules_RejectOutOfRangeValues(int minutes, double minReward, double maxReward, double chance, int count)
    {
        var location = new ExpeditionLocation
        {
            Id = "x", Name = "X", DurationMinutes = minutes, MinReward = minReward, MaxReward = maxReward,
            CardRewardChance = chance, CardRewardCount = count,
        };

        Assert.NotNull(ExpeditionLocationRules.Validate(location));
    }
}
