using DiscordBot.Services;

namespace DiscordBot.Models
{
    /// <summary>
    ///     Represents a session for navigating through a pack of Pokémon cards.
    /// </summary>
    public class PackSession
    {
        /// <summary>
        ///     Gets the message ID associated with the session.
        /// </summary>
        public ulong MessageId { get; }

        /// <summary>
        ///     Gets the user ID of the session participant.
        /// </summary>
        public ulong UserId { get; }

        /// <summary>
        ///     Gets the list of cards in the session.
        /// </summary>
        public List<Card> Cards { get; }

        /// <summary>
        ///     Gets or sets the current index of the displayed card.
        /// </summary>
        public int CurrentIndex { get; set; }

        /// <summary>
        ///     A set of identifiers (for example, a combination of card name and rarity)
        ///     representing cards in this pack that have already been saved.
        /// </summary>
        public HashSet<string> SavedCardIdentifiers { get; set; } = new();

        /// <summary>
        ///     Initializes a new instance of the <see cref="PackSession" /> class.
        /// </summary>
        /// <param name="messageId">The message ID associated with the session.</param>
        /// <param name="userId">The user ID of the session participant.</param>
        /// <param name="cards">The list of cards to navigate.</param>
        public PackSession(ulong messageId, ulong userId, List<Card> cards)
        {
            MessageId = messageId;
            UserId = userId;
            Cards = cards;
            CurrentIndex = 0;
        }
    }
}
