using System.Globalization;
using DiscordBot.Events.Expedition;
using DiscordBot.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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
        await using var connection = await database.OpenAsync();

        var collection = new UserCardCollection { UserId = userId };

        await using (var command = SqliteCommands.Create(connection, null, """
            SELECT balance_cents, packs_pulled, cards_traded, favorite_card_id, left_at_utc
            FROM users WHERE user_id = $userId;
            """, ("$userId", (long)userId)))
        {
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return collection;

            collection.Balance = reader.GetInt64(0) / 100m;
            collection.PacksPulled = reader.GetInt32(1);
            collection.CardsTraded = reader.GetInt32(2);
            collection.FavoriteCardId = reader.IsDBNull(3) ? null : reader.GetInt64(3);

            if (!reader.IsDBNull(4))
            {
                await reader.DisposeAsync();
                await SqliteCommands.ExecuteAsync(connection, null, "UPDATE users SET left_at_utc = NULL WHERE user_id = $userId;", ("$userId", (long)userId));
                logger.LogInformation("Restored archived collection of user {UserId}", userId);
            }
        }

        await using (var command = SqliteCommands.Create(connection, null, """
            SELECT id, set_id, local_id, name, rarity, image,
                   cardmarket_avg_cents, tcgplayer_market_cents, tcgplayer_low_cents, is_locked
            FROM cards WHERE user_id = $userId ORDER BY id;
            """, ("$userId", (long)userId)))
        {
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                collection.Cards.Add(ReadCard(reader));
        }

        await using (var command = SqliteCommands.Create(connection, null, """
            SELECT location_id, start_time_utc, end_time_utc
            FROM expeditions WHERE user_id = $userId;
            """, ("$userId", (long)userId)))
        {
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                collection.ActiveExpedition = new ActiveExpedition
                {
                    LocationId = reader.GetString(0),
                    StartTimeUtc = ParseUtc(reader.GetString(1)),
                    EndTimeUtc = ParseUtc(reader.GetString(2)),
                };
            }
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
        await using var connection = await database.OpenAsync();
        await using var transaction = connection.BeginTransaction();
        await SaveAsync(connection, transaction, collections);
        await transaction.CommitAsync();
    }

    /// <summary>
    ///     Saves collections inside a transaction the caller owns, see <see cref="SaveUserCardsAsync"/>.
    /// </summary>
    internal static async Task SaveAsync(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<UserCardCollection> collections)
    {
        // Rows and card ownership first, deletes last: a traded card is still owned by the sender
        // in the database until the receiver's save moves it, and must not be deleted on the way
        foreach (var collection in collections)
        {
            await SqliteCommands.ExecuteAsync(connection, transaction, """
                INSERT INTO users (user_id, balance_cents, packs_pulled, cards_traded, left_at_utc)
                VALUES ($userId, $balanceCents, $packsPulled, $cardsTraded, NULL)
                ON CONFLICT (user_id) DO UPDATE SET
                    balance_cents = excluded.balance_cents,
                    packs_pulled = excluded.packs_pulled,
                    cards_traded = excluded.cards_traded,
                    left_at_utc = NULL;
                """,
                ("$userId", (long)collection.UserId),
                ("$balanceCents", ToCents(collection.Balance)),
                ("$packsPulled", collection.PacksPulled),
                ("$cardsTraded", collection.CardsTraded));
        }

        foreach (var collection in collections)
        {
            foreach (var card in collection.Cards)
            {
                if (card.InstanceId == 0)
                    card.InstanceId = await InsertCardAsync(connection, transaction, collection.UserId, card);
                else
                    await SqliteCommands.ExecuteAsync(connection, transaction,
                        "UPDATE cards SET user_id = $userId, is_locked = $isLocked WHERE id = $id;",
                        ("$userId", (long)collection.UserId),
                        ("$isLocked", card.IsLocked),
                        ("$id", card.InstanceId));
            }
        }

        foreach (var collection in collections)
        {
            await SqliteCommands.ExecuteAsync(connection, transaction,
                "DELETE FROM cards WHERE user_id = $userId AND id NOT IN (SELECT value FROM json_each($ids));",
                ("$userId", (long)collection.UserId),
                ("$ids", JsonConvert.SerializeObject(collection.Cards.Select(c => c.InstanceId))));

            await SqliteCommands.ExecuteAsync(connection, transaction,
                "UPDATE users SET favorite_card_id = $favoriteCardId WHERE user_id = $userId;",
                ("$userId", (long)collection.UserId),
                ("$favoriteCardId", collection.FavoriteCard?.InstanceId));

            await SqliteCommands.ExecuteAsync(connection, transaction,
                "DELETE FROM expeditions WHERE user_id = $userId;",
                ("$userId", (long)collection.UserId));

            if (collection.ActiveExpedition is { } expedition)
            {
                await SqliteCommands.ExecuteAsync(connection, transaction, """
                    INSERT INTO expeditions (user_id, location_id, start_time_utc, end_time_utc)
                    VALUES ($userId, $locationId, $startTimeUtc, $endTimeUtc);
                    """,
                    ("$userId", (long)collection.UserId),
                    ("$locationId", expedition.LocationId),
                    ("$startTimeUtc", FormatUtc(expedition.StartTimeUtc)),
                    ("$endTimeUtc", FormatUtc(expedition.EndTimeUtc)));
            }
        }
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
        await using var connection = await database.OpenAsync();
        return await SqliteCommands.ExecuteAsync(connection, null,
            "UPDATE users SET left_at_utc = $now WHERE user_id = $userId;",
            ("$userId", (long)userId),
            ("$now", FormatUtc(DateTime.UtcNow))) > 0;
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
        await using var connection = await database.OpenAsync();
        // ISO 8601 strings in UTC compare in time order
        return await SqliteCommands.ExecuteAsync(connection, null,
            "DELETE FROM users WHERE left_at_utc IS NOT NULL AND left_at_utc < $cutoff;",
            ("$cutoff", FormatUtc(nowUtc - ArchiveRetention)));
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

    private static async Task<long> InsertCardAsync(SqliteConnection connection, SqliteTransaction transaction, ulong userId, Card card)
    {
        return (long)(await SqliteCommands.ScalarAsync(connection, transaction, """
            INSERT INTO cards (user_id, set_id, local_id, name, rarity, image,
                               cardmarket_avg_cents, tcgplayer_market_cents, tcgplayer_low_cents, is_locked)
            VALUES ($userId, $setId, $localId, $name, $rarity, $image,
                    $cardmarketAvgCents, $tcgplayerMarketCents, $tcgplayerLowCents, $isLocked)
            RETURNING id;
            """,
            ("$userId", (long)userId),
            ("$setId", card.SetId),
            ("$localId", card.LocalId),
            ("$name", card.Name),
            ("$rarity", card.Rarity),
            ("$image", card.Image),
            ("$cardmarketAvgCents", ToCents(card.Pricing?.Cardmarket?.Avg)),
            ("$tcgplayerMarketCents", ToCents(card.Pricing?.TcgPlayer?.Market)),
            ("$tcgplayerLowCents", ToCents(card.Pricing?.TcgPlayer?.Low)),
            ("$isLocked", card.IsLocked)))!;
    }

    private static Card ReadCard(SqliteDataReader reader)
    {
        double? cardmarketAvg = FromCents(reader, 6);
        double? tcgplayerMarket = FromCents(reader, 7);
        double? tcgplayerLow = FromCents(reader, 8);

        return new Card
        {
            InstanceId = reader.GetInt64(0),
            SetId = reader.IsDBNull(1) ? null! : reader.GetString(1),
            LocalId = reader.IsDBNull(2) ? null! : reader.GetString(2),
            Name = reader.GetString(3),
            Rarity = reader.IsDBNull(4) ? null! : reader.GetString(4),
            Image = reader.IsDBNull(5) ? null : reader.GetString(5),
            IsLocked = reader.GetBoolean(9),
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

    private static double? FromCents(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal) / 100.0;

    private static string FormatUtc(DateTime utc) => utc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTime ParseUtc(string text) => DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
