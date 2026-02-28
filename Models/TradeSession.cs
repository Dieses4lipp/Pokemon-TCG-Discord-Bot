using DiscordBot.Services;

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
public class TradeSession(ulong senderId, ulong receiverId, Card cardToTrade)
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
}