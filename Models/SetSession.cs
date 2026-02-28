namespace DiscordBot.Models;

/// <summary>
///     Represents a session for navigating through a list of Pokémon card sets.
/// </summary>
/// <param name="messageId">
///     The message ID associated with the session.
/// </param>
/// <param name="userId">
///     The user ID of the session participant.
/// </param>
/// <param name="sets">
///     The list of sets to navigate.
/// </param>
public class SetSession(ulong messageId, ulong userId, List<Set> sets)
{
    /// <summary>
    ///     Gets the message ID associated with the session.
    /// </summary>
    public ulong MessageId { get; } = messageId;

    /// <summary>
    ///     Gets the user ID of the participant in the session.
    /// </summary>
    public ulong UserId { get; } = userId;

    /// <summary>
    ///     Gets the list of sets in the session.
    /// </summary>
    public List<Set> Sets { get; } = sets;

    /// <summary>
    ///     Gets or sets the current index of the displayed set.
    /// </summary>
    public int CurrentIndex { get; set; }
}