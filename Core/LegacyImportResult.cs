namespace DiscordBot.Core;

/// <summary>
///     What <see cref="LegacyJsonImporter"/> moved into the database.
/// </summary>
/// <param name="Users">
///     Collections imported from <c>UserCards/</c>, archived ones included.
/// </param>
/// <param name="ArchivedUsers">
///     Of those, collections of users who had left (<c>UserCards/Left/</c>).
/// </param>
/// <param name="SkippedUsers">
///     Collections not imported because the database already had that user.
/// </param>
/// <param name="Cards">
///     Cards imported.
/// </param>
/// <param name="BotStateImported">
///     Whether <c>State/botState.json</c> was imported.
/// </param>
public sealed record LegacyImportResult(int Users, int ArchivedUsers, int SkippedUsers, int Cards, bool BotStateImported);
