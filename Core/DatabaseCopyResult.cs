namespace DiscordBot.Core;

/// <summary>
///     How many rows <see cref="SqliteToPostgresCopy"/> copied, per table.
/// </summary>
public sealed record DatabaseCopyResult(int Users, int Cards, int Expeditions, int Settings, int LockedSets, int ExpeditionLocations);
