using DiscordBot.Models;

namespace DiscordBot.Core;

/// <summary>
///     The outcome of carrying out a confirmed trade.
/// </summary>
/// <param name="Failure">
///     Why the trade failed; <see langword="null"/> if it went through.
/// </param>
/// <param name="CardFromSender">
///     The card that moved from the sender to the receiver.
/// </param>
/// <param name="CardFromReceiver">
///     The card that moved from the receiver to the sender, if one was requested.
/// </param>
public sealed record TradeResult(TradeFailure? Failure, Card? CardFromSender = null, Card? CardFromReceiver = null)
{
    /// <summary>
    ///     Gets whether the trade went through.
    /// </summary>
    public bool Succeeded => Failure is null;
}
