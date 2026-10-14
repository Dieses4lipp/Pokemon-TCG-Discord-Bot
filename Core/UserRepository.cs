using DiscordBot.Core.Storage;
using DiscordBot.Events.Expedition;
using DiscordBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Core;

/// <summary>
///     Loads and saves user card collections in the database.
/// </summary>
public sealed class UserRepository(BotDatabase database, ILogger<UserRepository> logger)
{
    /// <summary>
    ///     How long the collection of a user who left is kept before it is deleted for good.
    /// </summary>
    private static readonly TimeSpan ArchiveRetention = TimeSpan.FromDays(30);

    /// <summary>
    ///     Loads a user's card collection. A user who left a guild and is active again gets their
    ///     archived collection back.
    /// </summary>
    /// <param name="userId">
    ///     The user ID whose card collection is to be loaded.
    /// </param>
    /// <returns>
    ///     The user's card collection; an empty one if the user has none yet.
    /// </returns>
    public async Task<UserCardCollection> LoadUserCardsAsync(ulong userId)
    {
        await using var db = database.CreateContext();

        var collection = new UserCardCollection { UserId = userId };

        var user = await db.Users.FindAsync(userId);
        if (user == null)
            return collection;

        collection.Balance = user.BalanceCents / 100m;
        collection.PacksPulled = user.PacksPulled;
        collection.CardsTraded = user.CardsTraded;
        collection.FavoriteCardId = user.FavoriteCardId;

        if (user.LeftAtUtc != null)
        {
            user.LeftAtUtc = null;
            await db.SaveChangesAsync();
            logger.LogInformation("Restored archived collection of user {UserId}", userId);
        }

        collection.Cards = (await db.Cards.AsNoTracking().Where(c => c.UserId == userId).OrderBy(c => c.Id).ToListAsync())
            .Select(ToCard)
            .ToList();

        if (await db.Expeditions.AsNoTracking().SingleOrDefaultAsync(e => e.UserId == userId) is { } expedition)
        {
            collection.ActiveExpedition = new ActiveExpedition
            {
                LocationId = expedition.LocationId,
                StartTimeUtc = expedition.StartTimeUtc,
                EndTimeUtc = expedition.EndTimeUtc,
            };
        }

        return collection;
    }

    /// <summary>
    ///     Saves one or more collections in a single transaction, so a trade either moves both
    ///     sides or neither. New cards (<see cref="Card.InstanceId"/> 0) get their ID assigned;
    ///     cards missing from a collection are deleted unless another saved collection now holds them.
    /// </summary>
    /// <param name="collections">
    ///     The collections to save.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    public async Task SaveUserCardsAsync(params UserCardCollection[] collections)
    {
        await using var db = database.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await SaveAsync(db, collections);
        await transaction.CommitAsync();
    }

    /// <summary>
    ///     Saves collections inside a transaction the caller owns, see <see cref="SaveUserCardsAsync"/>.
    /// </summary>
    internal static async Task SaveAsync(BotDbContext db, IReadOnlyList<UserCardCollection> collections)
    {
        // Rows and card ownership first, deletes last: a traded card is still owned by the sender
        // in the database until the receiver's save moves it, and must not be deleted on the way
        foreach (var collection in collections)
        {
            var user = await db.Users.FindAsync(collection.UserId);
            if (user == null)
            {
                user = new UserRow { UserId = collection.UserId };
                db.Users.Add(user);
            }

            user.BalanceCents = ToCents(collection.Balance);
            user.PacksPulled = collection.PacksPulled;
            user.CardsTraded = collection.CardsTraded;
            user.LeftAtUtc = null;
        }
        await db.SaveChangesAsync();

        var savedIds = collections.SelectMany(c => c.Cards).Select(c => c.InstanceId).Where(id => id != 0).ToList();
        var existingRows = await db.Cards.Where(c => savedIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
        var newRows = new List<(Card Card, CardRow Row)>();
        foreach (var collection in collections)
        {
            foreach (var card in collection.Cards)
            {
                if (card.InstanceId == 0)
                {
                    var row = ToRow(collection.UserId, card);
                    db.Cards.Add(row);
                    newRows.Add((card, row));
                }
                else if (existingRows.TryGetValue(card.InstanceId, out var row))
                {
                    row.UserId = collection.UserId;
                    row.IsLocked = card.IsLocked;
                }
            }
        }
        await db.SaveChangesAsync();
        foreach (var (card, row) in newRows)
            card.InstanceId = row.Id;

        foreach (var collection in collections)
        {
            ulong userId = collection.UserId;
            var keptIds = collection.Cards.Select(c => c.InstanceId).ToList();
            long? favoriteCardId = collection.FavoriteCard?.InstanceId;

            await db.Cards.Where(c => c.UserId == userId && !keptIds.Contains(c.Id)).ExecuteDeleteAsync();
            await db.Users.Where(u => u.UserId == userId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.FavoriteCardId, favoriteCardId));
            await db.Expeditions.Where(e => e.UserId == userId).ExecuteDeleteAsync();

            if (collection.ActiveExpedition is { } expedition)
            {
                db.Expeditions.Add(new ExpeditionRow
                {
                    UserId = userId,
                    LocationId = expedition.LocationId,
                    StartTimeUtc = expedition.StartTimeUtc,
                    EndTimeUtc = expedition.EndTimeUtc,
                });
            }
        }
        await db.SaveChangesAsync();

        // The bulk deletes and updates above bypass the change tracker, so what it holds is stale;
        // a second save on the same context (the import does one per favorite) must start clean
        db.ChangeTracker.Clear();
    }

    /// <summary>
    ///     Marks a user's collection as left instead of deleting it. The next load restores it, so
    ///     a user still active in another guild (or rejoining) keeps everything; otherwise
    ///     <see cref="RunArchivePurgeLoopAsync"/> deletes it after <see cref="ArchiveRetention"/>.
    /// </summary>
    /// <param name="userId">
    ///     The user ID whose card collection is to be archived.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if the user had a collection.
    /// </returns>
    public async Task<bool> ArchiveUserCardsAsync(ulong userId)
    {
        await using var db = database.CreateContext();
        DateTime? now = DateTime.UtcNow;
        return await db.Users.Where(u => u.UserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.LeftAtUtc, now)) > 0;
    }

    /// <summary>
    ///     Deletes the collections of users who left more than <see cref="ArchiveRetention"/> ago.
    /// </summary>
    /// <param name="nowUtc">
    ///     The current time.
    /// </param>
    /// <returns>
    ///     The number of collections deleted.
    /// </returns>
    public async Task<int> PurgeArchivedUsersAsync(DateTime nowUtc)
    {
        await using var db = database.CreateContext();
        DateTime? cutoff = nowUtc - ArchiveRetention;
        // Cards and expedition go with the user (ON DELETE CASCADE). On SQLite the times are ISO
        // 8601 text in UTC, which compares in time order
        return await db.Users.Where(u => u.LeftAtUtc != null && u.LeftAtUtc < cutoff).ExecuteDeleteAsync();
    }

    /// <summary>
    ///     Runs <see cref="PurgeArchivedUsersAsync"/> now and then once a day. Never throws.
    /// </summary>
    /// <returns>
    ///     A task that runs for the lifetime of the process.
    /// </returns>
    public async Task RunArchivePurgeLoopAsync()
    {
        while (true)
        {
            try
            {
                int purged = await PurgeArchivedUsersAsync(DateTime.UtcNow);
                if (purged > 0)
                    logger.LogInformation("Purged {Count} collections of users who left more than {RetentionDays} days ago",
                        purged, ArchiveRetention.TotalDays);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to purge archived collections");
            }

            await Task.Delay(TimeSpan.FromDays(1));
        }
    }

    private static CardRow ToRow(ulong userId, Card card) => new()
    {
        UserId = userId,
        SetId = card.SetId,
        LocalId = card.LocalId,
        Name = card.Name,
        Rarity = card.Rarity,
        Image = card.Image,
        CardmarketAvgCents = ToCents(card.Pricing?.Cardmarket?.Avg),
        TcgplayerMarketCents = ToCents(card.Pricing?.TcgPlayer?.Market),
        TcgplayerLowCents = ToCents(card.Pricing?.TcgPlayer?.Low),
        IsLocked = card.IsLocked,
    };

    private static Card ToCard(CardRow row)
    {
        double? cardmarketAvg = FromCents(row.CardmarketAvgCents);
        double? tcgplayerMarket = FromCents(row.TcgplayerMarketCents);
        double? tcgplayerLow = FromCents(row.TcgplayerLowCents);

        return new Card
        {
            InstanceId = row.Id,
            SetId = row.SetId!,
            LocalId = row.LocalId!,
            Name = row.Name,
            Rarity = row.Rarity!,
            Image = row.Image,
            IsLocked = row.IsLocked,
            Pricing = cardmarketAvg == null && tcgplayerMarket == null && tcgplayerLow == null
                ? null
                : new CardPricing
                {
                    Cardmarket = cardmarketAvg == null ? null : new CardmarketPricing { Avg = cardmarketAvg },
                    TcgPlayer = tcgplayerMarket == null && tcgplayerLow == null
                        ? null
                        : new TcgPlayerPricing { Market = tcgplayerMarket, Low = tcgplayerLow },
                },
        };
    }

    private static long ToCents(decimal amount) => (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);

    private static long? ToCents(double? price) => price is { } value ? ToCents(Money.FromDouble(value)) : null;

    private static double? FromCents(long? cents) => cents / 100.0;
}
