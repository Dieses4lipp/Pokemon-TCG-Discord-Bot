namespace DiscordBot.Models;

/// <summary>
///     Represents a trade session between two users.
/// </summary>
/// <param name="senderId">
///     The ID of the user who is sending the card.
/// </param>
/// <param name="receiverId">
///     The ID of the user who is receiving the card.
/// </param>
/// <param name="cardToTrade">
///     The card being traded.
/// </param>
/// <param name="cardToReceive">
///     The card being received.
/// </param>
public class TradeSession(ulong senderId, ulong receiverId, Card cardToTrade, Card cardToReceive)
{
    /// <summary>
    ///     Gets the ID of the user who is sending the card.
    /// </summary>
    public ulong SenderId { get; } = senderId;

    /// <summary>
    ///     Gets the ID of the user who is receiving the card.
    /// </summary>
    public ulong ReceiverId { get; } = receiverId;

    /// <summary>
    ///     Gets the card that is being traded.
    /// </summary>
    public Card CardToTrade { get; } = cardToTrade;

    /// <summary>
    ///     Gets the card that is being received.
    /// </summary>
    public Card CardToReceive { get; } = cardToReceive;
}