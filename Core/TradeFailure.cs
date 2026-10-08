namespace DiscordBot.Core;

/// <summary>
///     Why a confirmed trade could not be carried out.
/// </summary>
public enum TradeFailure
{
    /// <summary>
    ///     The sender no longer owns the offered card.
    /// </summary>
    SenderCardMissing,

    /// <summary>
    ///     The offered card was sent on an expedition after the trade was proposed.
    /// </summary>
    SenderCardLocked,

    /// <summary>
    ///     The receiver no longer owns the requested card.
    /// </summary>
    ReceiverCardMissing,

    /// <summary>
    ///     The requested card was sent on an expedition after the trade was proposed.
    /// </summary>
    ReceiverCardLocked,

    /// <summary>
    ///     The receiver can no longer pay the requested money.
    /// </summary>
    ReceiverBalanceTooLow,
}
