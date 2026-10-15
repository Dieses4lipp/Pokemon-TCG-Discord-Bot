namespace DiscordBot.Core.Storage;

/// <summary>
///     A row of the <c>users</c> table: one Discord user's balance and counters.
/// </summary>
public sealed class UserRow
{
    public ulong UserId { get; set; }

    public long BalanceCents { get; set; }

    public int PacksPulled { get; set; }

    public int CardsTraded { get; set; }

    public long? FavoriteCardId { get; set; }

    /// <summary>
    ///     Gets or sets when the user left the last guild; <see langword="null"/> while active.
    /// </summary>
    public DateTime? LeftAtUtc { get; set; }
}
