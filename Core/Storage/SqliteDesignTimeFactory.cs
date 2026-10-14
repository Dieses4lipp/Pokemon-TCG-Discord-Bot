using Microsoft.EntityFrameworkCore.Design;

namespace DiscordBot.Core.Storage;

/// <summary>
///     Lets <c>dotnet ef</c> build the SQLite model without starting the bot.
/// </summary>
public sealed class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<SqliteBotDbContext>
{
    public SqliteBotDbContext CreateDbContext(string[] args) => SqliteBotDbContext.Create("design-time.db");
}
