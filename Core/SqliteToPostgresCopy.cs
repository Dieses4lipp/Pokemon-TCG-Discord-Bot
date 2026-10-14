using DiscordBot.Core.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Core;

/// <summary>
///     Copies every row of an SQLite database into an empty Postgres database, IDs included, for the
///     one-time move to Postgres (<c>--migrate-sqlite-to-postgres</c>). Run it while the bot is stopped.
/// </summary>
public static class SqliteToPostgresCopy
{
    /// <summary>
    ///     Brings both schemas up to date, then copies in one Postgres transaction: all or nothing.
    ///     A legacy SQLite file is taken over by EF Core on the way, see <see cref="BotDatabase.MigrateAsync"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     The SQLite file does not exist, or the Postgres database already holds bot data.
    /// </exception>
    public static async Task<DatabaseCopyResult> CopyAsync(BotDatabase source, BotDatabase target, ILogger logger)
    {
        if (source.Provider != DatabaseProvider.Sqlite || target.Provider != DatabaseProvider.Postgres)
            throw new ArgumentException("The copy goes from an SQLite database to a Postgres database.");
        // Migrating a missing file would create an empty one and "copy" nothing
        if (!File.Exists(source.SqlitePath))
            throw new InvalidOperationException($"There is no SQLite database at '{source.SqlitePath}'.");

        await source.MigrateAsync(logger);
        await target.MigrateAsync(logger);

        await using var from = source.CreateContext();
        await using var to = target.CreateContext();

        if (await to.Users.AnyAsync() || await to.Cards.AnyAsync() || await to.Expeditions.AnyAsync() ||
            await to.Settings.AnyAsync() || await to.LockedSets.AnyAsync() || await to.ExpeditionLocations.AnyAsync())
            throw new InvalidOperationException($"{target.Description} already holds bot data; the copy only goes into an empty database.");

        var users = await from.Users.AsNoTracking().ToListAsync();
        var cards = await from.Cards.AsNoTracking().ToListAsync();
        var expeditions = await from.Expeditions.AsNoTracking().ToListAsync();
        var settings = await from.Settings.AsNoTracking().ToListAsync();
        var lockedSets = await from.LockedSets.AsNoTracking().ToListAsync();
        var locations = await from.ExpeditionLocations.AsNoTracking().ToListAsync();

        await using var transaction = await to.Database.BeginTransactionAsync();

        // users.favorite_card_id and cards.user_id point at each other: users without favorites
        // first, then the cards, then the favorites
        var favorites = users.Where(u => u.FavoriteCardId != null).ToDictionary(u => u.UserId, u => u.FavoriteCardId);
        foreach (var user in users)
            user.FavoriteCardId = null;
        to.Users.AddRange(users);
        await to.SaveChangesAsync();

        to.Cards.AddRange(cards);
        await to.SaveChangesAsync();

        foreach (var user in users)
            user.FavoriteCardId = favorites.GetValueOrDefault(user.UserId);
        to.Expeditions.AddRange(expeditions);
        to.Settings.AddRange(settings);
        to.LockedSets.AddRange(lockedSets);
        to.ExpeditionLocations.AddRange(locations);
        await to.SaveChangesAsync();

        // The card IDs were inserted as they were; new cards must continue after the highest one
        await to.Database.ExecuteSqlRawAsync(
            $"SELECT setval(pg_get_serial_sequence('{PostgresBotDbContext.Schema}.cards', 'id'), " +
            $"COALESCE((SELECT MAX(id) FROM {PostgresBotDbContext.Schema}.cards), 0) + 1, false);");

        await transaction.CommitAsync();

        return new DatabaseCopyResult(users.Count, cards.Count, expeditions.Count, settings.Count, lockedSets.Count, locations.Count);
    }
}
