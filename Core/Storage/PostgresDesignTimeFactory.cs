using Microsoft.EntityFrameworkCore.Design;

namespace DiscordBot.Core.Storage;

/// <summary>
///     Lets <c>dotnet ef</c> build the Postgres model without starting the bot. Adding a migration
///     never connects, so the connection string only has to parse.
/// </summary>
public sealed class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<PostgresBotDbContext>
{
    public PostgresBotDbContext CreateDbContext(string[] args) => PostgresBotDbContext.Create("Host=localhost;Database=pokebot");
}
