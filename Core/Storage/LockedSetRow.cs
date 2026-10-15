namespace DiscordBot.Core.Storage;

/// <summary>
///     A row of the <c>locked_sets</c> table: a set that cannot be pulled.
/// </summary>
public sealed class LockedSetRow
{
    public string SetId { get; set; } = default!;
}
