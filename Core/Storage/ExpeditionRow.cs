namespace DiscordBot.Core.Storage;

/// <summary>
///     A row of the <c>expeditions</c> table: the running expedition of a user.
/// </summary>
public sealed class ExpeditionRow
{
    public ulong UserId { get; set; }

    public string LocationId { get; set; } = default!;

    public DateTime StartTimeUtc { get; set; }

    public DateTime EndTimeUtc { get; set; }
}
