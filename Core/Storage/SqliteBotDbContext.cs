using DiscordBot.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Core.Storage;

/// <summary>
///     The SQLite flavor of <see cref="BotDbContext"/>, with migrations in <c>Migrations/Sqlite</c>.
/// </summary>
public sealed class SqliteBotDbContext(DbContextOptions<SqliteBotDbContext> options) : BotDbContext(options)
{
    /// <summary>
    ///     Creates a context on the database file at <paramref name="path"/>, foreign keys enforced.
    /// </summary>
    public static SqliteBotDbContext Create(string path)
    {
        string connectionString = new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true }.ToString();
        return new SqliteBotDbContext(new DbContextOptionsBuilder<SqliteBotDbContext>().UseSqlite(connectionString).Options);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcIsoStringConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // IDs are typed by admins; "Mt-Moon" and "mt-moon" are the same location
        modelBuilder.Entity<ExpeditionLocation>().Property(l => l.Id).UseCollation("NOCASE");
    }
}
