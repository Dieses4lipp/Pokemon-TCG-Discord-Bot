namespace DiscordBot.Core.Storage;

/// <summary>
///     A row of the <c>cards</c> table: one copy of a card owned by a user.
/// </summary>
public sealed class CardRow
{
    public long Id { get; set; }

    public ulong UserId { get; set; }

    public string? SetId { get; set; }

    public string? LocalId { get; set; }

    public string Name { get; set; } = default!;

    public string? Rarity { get; set; }

    public string? Image { get; set; }

    public long? CardmarketAvgCents { get; set; }

    public long? TcgplayerMarketCents { get; set; }

    public long? TcgplayerLowCents { get; set; }

    public bool IsLocked { get; set; }
}
