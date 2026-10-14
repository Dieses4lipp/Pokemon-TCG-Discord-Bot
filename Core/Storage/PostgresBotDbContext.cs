using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Core.Storage;

/// <summary>
///     The Postgres flavor of <see cref="BotDbContext"/>, with migrations in <c>Migrations/Postgres</c>.
///     Everything, the migration history included, lives in the <see cref="Schema"/> schema, so the
///     bot can share a database with other apps.
/// </summary>
public sealed class PostgresBotDbContext(DbContextOptions<PostgresBotDbContext> options) : BotDbContext(options)
{
    public const string Schema = "pokebot";

    /// <summary>
    ///     Creates a context on the database <paramref name="connectionString"/> points to.
    /// </summary>
    public static PostgresBotDbContext Create(string connectionString) =>
        new(new DbContextOptionsBuilder<PostgresBotDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", Schema))
            .Options);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
    }
}
