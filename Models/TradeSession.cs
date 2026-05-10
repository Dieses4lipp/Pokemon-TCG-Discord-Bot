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
///     The card being received (can be null if trading strictly for money).
/// </param>
/// <param name="moneyToReceive">
///     The amount of money the sender wants to receive.
/// </param>
public class TradeSession(ulong senderId, ulong receiverId, Card cardToTrade, Card? cardToReceive, double moneyToReceive)
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
    ///     Gets the card that is being received, if any.
    /// </summary>
    public Card? CardToReceive { get; } = cardToReceive;

    /// <summary>
    ///     Gets the amount of money requested, if any.
    /// </summary>
    public double MoneyToReceive { get; } = moneyToReceive;
}